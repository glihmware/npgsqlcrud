using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;


namespace NpgsqlCrud
{
    public static class QueryBuilder
    {

        /// <summary>
        ///   Select fields for a read operation based on the property
        ///   attributes of object of type T.
        /// </summary>
        /// <typeparam name="T"> Type from where properties are parsed. </typeparam>
        /// <param name="onlySelectFields">
        ///   Fields to be selected in the query.
        ///   If provided, only properties in fields will be selected.
        /// </param>
        /// <returns> SQL fields select part of the query. </returns>
        public static string SelectFieldFromProps<T>(string[] onlySelectFields = null)
        {
            return _fieldSelector(GetProps<T>(NpgsqlCrudOp.Read, onlySelectFields: onlySelectFields));
        }

        /// <summary>
        ///   Lists all the properties candidate to be fields in a SQL query.
        ///   Properties are filtered based on their attributes (DbField...).
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="op"></param>
        /// <param name="toLowerCase"></param>
        /// <param name="o"></param>
        /// <returns></returns>
        public static string[]
            GetProps<T>(
            NpgsqlCrudOp op,
            string[] onlySelectFields = null,
            bool toLowerCase = true,
            object o = null)
        {
            var props = new List<string>();

            // Enforce selected fields to be camelcase
            // as we iterate on props to check the names.
            if (onlySelectFields != null)
            {
                for (int i = 0; i < onlySelectFields.Length; i++)
                    onlySelectFields[i] = CaseFormatter.LowerCaseToCamelCase(onlySelectFields[i]);
            }

            foreach (PropertyInfo prop in typeof(T).GetProperties())
            {
                string n = prop.Name;
                if (toLowerCase)
                {
                    n = CaseFormatter.CamelCaseToLowerCase(n);
                }

                // Need to defined if OnlyFields is always CamelCase or LowerCase...???
                // Should be better CamelCase as this is called from C#.

                // If applies, skip the fields that are not selected explicitly.
                if (onlySelectFields != null && !onlySelectFields.Contains(prop.Name)) continue;

                // Check the attributes based on the operation.
                if (_hasAttribute(prop, typeof(DbFieldInternal))) continue;

                switch (op)
                {
                    default:
                    case NpgsqlCrudOp.Read:
                        if (_hasAttribute(prop, typeof(DbFieldIgnoreRead))) continue;

                        if (_hasAttribute(prop, typeof(DbFieldOptionalFk)))
                        {
                            n = $"coalesce({n}::int, 0) AS {n}";
                        }
                        break;

                    case NpgsqlCrudOp.Write:
                        if (_hasAttribute(prop, typeof(DbFieldIgnoreWrite))) continue;

                        if (_hasAttribute(prop, typeof(DbFieldOptionalFk))
                            && o != null
                            && (int)prop.GetValue(o, null) == 0) continue;
                        break;
                }

                props.Add(n);
            }

            return props.ToArray();
        }

        ///
        public static string
            QueryCreate<T>(T t, string[] onlyFields = null)
        {
            string[] fields;
            string[] props;

            fields = GetProps<T>(NpgsqlCrudOp.Write, o: t, onlySelectFields: onlyFields);
            props = GetProps<T>(NpgsqlCrudOp.Write, toLowerCase: false, o: t, onlySelectFields: onlyFields);

            string table = _getTableName<T>();

            string strf = "(";
            string vals = "(";
            bool hasId = false;
            for (int i = 0; i < fields.Length; i++)
            {
                string f = fields[i];
                PropertyInfo p = t.GetType().GetProperty(props[i]);
                if (p == null)
                {
                    throw new Exception(
                        $"<{typeof(T).Name}> doesn't have <{props[i]}> property.");
                }

                // Skip the ID, almost always auto incremented in database..
                if (p.Name == "Id")
                {
                    hasId = true;
                    continue;
                }

                object v = p.GetValue(t, null);

                // Convert enum type to the specified type in the attribute (int, long ...)
                DbFieldEnumTypeCast _tc = Attribute.GetCustomAttribute(p, typeof(DbFieldEnumTypeCast)) as DbFieldEnumTypeCast;
                if (_tc != null) v = Convert.ChangeType(v, _tc.DbType);

                if (v != null && v.GetType() == typeof(string))
                {
                    v = v.ToString().Replace("'", "''");
                }

                if (i < fields.Length - 1)
                {
                    strf += $"{f}, ";
                    vals += $"'{v}', ";
                }
                else
                {
                    strf += $"{f})";
                    vals += $"'{v}')";
                }
            }

            string insert_s = $"INSERT INTO {table} {strf} VALUES {vals}";
            if (hasId)
            {
                insert_s += " RETURNING id";
            }
            else
            {
                insert_s += " RETURNING 0";
            }

            return insert_s;
        }


        ///
        public static string
            QueryCount<T>(WhereBuilder wb)
        {
            string table = _getTableName<T>();
            return $"SELECT COUNT(id) FROM {table} {wb}";
        }

        ///
        public static string
            QueryRead<T>(int id, string[] onlyFields = null)
        {
            string fstr = SelectFieldFromProps<T>(onlyFields);
            string table = _getTableName<T>();

            return $"SELECT {fstr} FROM {table} WHERE (id = {id})";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="wb"></param>
        /// <param name="onlyFields"></param>
        /// <returns></returns>
        public static string
            QueryRead<T>(WhereBuilder wb = null, string[] onlyFields = null)
        {
            string fstr = SelectFieldFromProps<T>(onlyFields);
            string table = _getTableName<T>();

            string q = $"SELECT {fstr} FROM {table}";
            if (wb != null) q += wb.ToString();

            return q;
        }

        ///
        public static string
            QueryDelete<T>(long id)
        {
            string table = _getTableName<T>();

            return $"DELETE FROM {table} WHERE (id = {id})";
        }

        public static string
            QueryDelete<T>(WhereBuilder wb = null, bool force = false)
        {
            string table = _getTableName<T>();

            string where = "";
            if (wb != null)
            {
                where = wb.ToString();
            }
            else
            {
                if (!force)
                    throw new Exception(@"You are calling DELETE FROM without any where clause.
                                          Set 'force: true' if it's something you are aware.");
            }

            return $"DELETE FROM {table} {where}";
        }



        ///
        public static string
            QueryUpdate<T>(long id, T t, string[] onlyFields = null)
        {
            string[] fields;
            string[] props;

            fields = GetProps<T>(NpgsqlCrudOp.Write, o: t, onlySelectFields: onlyFields);
            props = GetProps<T>(NpgsqlCrudOp.Write, toLowerCase: false, o: t, onlySelectFields: onlyFields);

            string table = _getTableName<T>();

            string sets = "SET ";

            for (int i = 0; i < fields.Length; i++)
            {
                string f = fields[i];
                PropertyInfo p = t.GetType().GetProperty(props[i]);
                if (p == null)
                {
                    throw new Exception(
                        $"<{typeof(T).Name}> doesn't have <{props[i]}> property.");
                }

                // Skip the ID, as it's not updatable in the current implementaion.
                if (p.Name == "Id") continue;

                object v = p.GetValue(t, null);

                DbFieldEnumTypeCast _tc = Attribute.GetCustomAttribute(p, typeof(DbFieldEnumTypeCast)) as DbFieldEnumTypeCast;
                if (_tc != null) v = Convert.ChangeType(v, _tc.DbType);

                if (v != null && v.GetType() == typeof(string))
                {
                    v = v.ToString().Replace("'", "''");
                }

                sets += $"{f} = '{v}'";

                if (i < fields.Length - 1) sets += ", ";
            }

            return $"UPDATE {table} {sets} WHERE (id = {id})";
        }



        ///
        public static string
            QueryUpdate<T>(WhereBuilder wb, T t, string[] onlyFields = null)
        {
            string[] fields;
            string[] props;

            fields = GetProps<T>(NpgsqlCrudOp.Write, o: t, onlySelectFields: onlyFields);
            props = GetProps<T>(NpgsqlCrudOp.Write, toLowerCase: false, o: t, onlySelectFields: onlyFields);

            string table = _getTableName<T>();

            string sets = "SET ";

            for (int i = 0; i < fields.Length; i++)
            {
                string f = fields[i];
                PropertyInfo p = t.GetType().GetProperty(props[i]);
                if (p == null)
                {
                    throw new Exception(
                        $"<{typeof(T).Name}> doesn't have <{props[i]}> property.");
                }

                // Skip the ID, as it's not updatable in the current implementaion.
                if (p.Name == "Id") continue;

                object v = p.GetValue(t, null);

                DbFieldEnumTypeCast _tc = Attribute.GetCustomAttribute(p, typeof(DbFieldEnumTypeCast)) as DbFieldEnumTypeCast;
                if (_tc != null) v = Convert.ChangeType(v, _tc.DbType);

                if (v != null && v.GetType() == typeof(string))
                {
                    v = v.ToString().Replace("'", "''");
                }

                sets += $"{f} = '{v}'";

                if (i < fields.Length - 1) sets += ", ";
            }

            return $"UPDATE {table} {sets} {wb}";
        }


        /// <summary>
        ///   Builds SQL fields selection.
        /// </summary>
        /// <param name="fields"></param>
        /// <returns></returns>
        private static string _fieldSelector(string[] fields = null)
        {
            if (fields == null) return " * ";

            string s = "";
            for (int i = 0; i < fields.Length; i++)
            {
                if (i < fields.Length - 1)
                {
                    s += $"{fields[i]}, ";
                }
                else
                {
                    s += $"{fields[i]}";
                }
            }

            return s;
        }


        /// <summary>
        ///   Computes the table name from the object type.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        private static string _getTableName<T>()
        {
            return CaseFormatter.CamelCaseToLowerCase(typeof(T).Name);
        }

        /// <summary>
        ///   Verifies if the given property has the given attribute.
        /// </summary>
        /// <param name="pi"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        private static bool _hasAttribute(PropertyInfo pi, Type t)
        {
            var a = Attribute.GetCustomAttribute(pi, t);
            return a != null;
        }
    }
}
