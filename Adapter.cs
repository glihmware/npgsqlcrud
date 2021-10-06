using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Npgsql;

namespace NpgsqlCrud
{

    ///
    public static class Adapter
    {
        /// <summary>
        ///   Converter for Db field name to C# property name.
        /// </summary>
        /// <param name="dbField"></param>
        /// <returns></returns>
        public delegate string DbFieldToPropertyConverter(string dbField);

        /// <summary>
        /// 
        /// </summary>
        public static string DefaultConnStr;
        
        /// <summary>
        /// 
        /// </summary>
        public static string LogPath;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public static async Task
        LogToFile(object o)
        {
            if (string.IsNullOrEmpty(LogPath))
            {
                Console.WriteLine("No logpath in NPGSQL adapter.");
                return;
            }

            try
            {
                await File.AppendAllTextAsync(LogPath, o.ToString() + "\n");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }


        /// <summary>
        ///   Opens a connection from the provided environment variable.
        /// </summary>
        /// <returns></returns>
        public static async Task<NpgsqlConnection> OpenFromEnv(string varEnv = "DB_VARENV")
        {
            if (!string.IsNullOrEmpty(DefaultConnStr))
            {
                varEnv = DefaultConnStr;
            }

            string connstr = Environment.GetEnvironmentVariable(varEnv);
            if (string.IsNullOrEmpty(connstr))
            {
                throw new Exception($"Can't init connection from '{varEnv}' => '{connstr}'.");
            }

            NpgsqlConnection conn = new NpgsqlConnection(connstr);
            await conn.OpenAsync();

            return conn;
        }

        /// <summary>
        ///   Reuse the connection if not null, otherwise open a new connection from env default.
        /// </summary>
        /// <param name="conn"></param>
        /// <returns> True if a new connection was opened, false otherwise. </returns>
        public static async Task<bool> ReuseOrOpenFromEnv(NpgsqlConnection conn)
        {
            if (conn == null)
            {
                conn = await OpenFromEnv();
                return true;
            }

            return false;
        }

        /// <summary>
        ///   Convert the the rows in the reader to an instance of T.
        /// </summary>
        /// <typeparam name="T">
        ///   Type of the instance to be created.
        ///   Must declare a parameterless constructor.
        /// </typeparam>
        /// <param name="reader"> Npgsql reader instance. </param>
        /// <param name="fieldNameToPropertyConverter">
        ///   Delegate to be called in order to convert a database field name to a C# property name.
        ///   If null, the default LowerCaseToCamelCase will be used (field_name => FieldName).
        /// </param>
        /// <returns> List of T instances. (Empty list if no rows were found) </returns>
        public async static Task<List<T>> ToObjectsAsync<T>(
            NpgsqlDataReader reader,
            DbFieldToPropertyConverter fieldNameToPropertyConverter = null
        ) where T : class, new()
        {
            List<string> props = new List<string>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                string fieldCs = _propertyNameFromDbFieldName(
                    reader.GetName(i),
                    fieldNameToPropertyConverter
                );
                props.Add(fieldCs);
            }

            List<T> res = new List<T>();

            while (await reader.ReadAsync())
            {
                res.Add(_toObject<T>(props, reader));
            }
            return res;
        }

        /// <summary>
        ///   Convert the first row in the reader to an instance of T.
        /// </summary>
        /// <typeparam name="T">
        ///   Type of the instance to be created.
        ///   Must declare a parameterless constructor.
        /// </typeparam>
        /// <param name="reader"> Npgsql reader instance. </param>
        /// <param name="fieldNameToPropertyConverter">
        ///   Delegate to be called in order to convert a database field name to a C# property name.
        ///   If null, the default LowerCaseToCamelCase will be used (field_name => FieldName).
        /// </param>
        /// <returns>
        ///   Instance of T if the first row can be converted to T type, null otherwise.
        /// </returns>
        public async static Task<T> ToObjectAsync<T>(
            NpgsqlDataReader reader,
            DbFieldToPropertyConverter fieldNameToPropertyConverter = null
        ) where T : class, new()
        {
            List<string> props = new List<string>();

            for (int i = 0; i < reader.FieldCount; i++)
            {
                // From the the column name, match the expected C# property name.
                string fieldCs = _propertyNameFromDbFieldName(
                    reader.GetName(i),
                    fieldNameToPropertyConverter
                );
                props.Add(fieldCs);
            }

            while (await reader.ReadAsync())
            {
                return _toObject<T>(props, reader);
            }

            return null;
        }

        /// <summary>
        ///   Converts a db field name to C# field name using the converter (if provided).
        /// </summary>
        /// <param name="dbFieldName"> Db field to be converted. </param>
        /// <param name="converter">
        ///   If provided, the delegate used to convert a db field name to C# property name.
        ///   If null, the default LowerCaseToCamelCase is user (field_name => FieldName).
        /// </param>
        /// <returns></returns>
        private static string
            _propertyNameFromDbFieldName(string dbFieldName, DbFieldToPropertyConverter converter)
        {
            string fieldCs;
            if (converter == null)
            {
                fieldCs = CaseFormatter.LowerCaseToCamelCase(dbFieldName);
            }
            else
            {
                fieldCs = converter(dbFieldName);
            }

            return fieldCs;
        }

        /// <summary>
        ///   Converts a row to an instance of T.
        ///
        /// </summary>
        /// <typeparam name="T">
        ///   Type of the instance to be created.
        ///   Must declare a parameterless constructor.
        /// </typeparam>
        /// <param name="props"> List of properties expected as column name. </param>
        /// <param name="reader"> Npgsql reader instance. </param>
        /// <returns> Instance of T on success, null otherwise. </returns>
        private static T _toObject<T>(
            List<string> props,
            NpgsqlDataReader reader
        ) where T : class, new()
        {
            T t = new T();
            for (int i = 0; i < props.Count(); i++)
            {
                PropertyInfo pi = t.GetType().GetProperty(props[i]);
                if (pi == null)
                {
                    throw new Exception($@"Type '{t.GetType()}' is expected a column named '{props[i]}'.
                    Verify if getter/setter (get;set;) are present in the class properties.
                    Also double check the field name in the database.");
                }

                object val;

                // DbFieldEnumTypeCast allow a cast from integer types to C# enumeration.
                Attribute _a = Attribute.GetCustomAttribute(pi, typeof(DbFieldEnumTypeCast));
                if (_a != null)
                {
                    if (!pi.PropertyType.IsEnum) throw new Exception($@"
                        '{pi.PropertyType}' is not an enum. DbFieldEnumTypeCast is only supported for enum types.
                        Remove the [DbFieldEnumTypeCast] attribute of change the type of '{pi.Name}'.
                        ");

                    val = Enum.Parse(pi.PropertyType, reader[i].ToString());
                }
                else
                {
                    // Npgsql type mapping (https://www.npgsql.org/doc/types/basic.html).
                    val = reader[i];
                }

                pi.SetValue(t, val, null);
            }

            return t;
        }



    }

}
