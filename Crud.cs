using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Npgsql;

namespace NpgsqlCrud
{

    /// <summary>
    ///   Operation types that can be done on the database.
    /// </summary>
    public enum NpgsqlCrudOp
    {
        Read,
        Write,
    }

    /// <summary>
    ///   Reason / return code from database related call.
    /// </summary>
    public enum NpgsqlCrudErrno
    {
        Ok = 0,
        Generic = -1,
        DuplicateKey = -2,
    }


    /// <summary>
    ///   A field flagged as IgnoreWrite will be removed from
    ///   a query associated with a write operation.
    /// </summary>
    public class DbFieldIgnoreWrite : Attribute { }

    /// <summary>
    ///   A field flagged as IgnoreRead will be removed from
    ///   a query associated with a read operation.
    /// </summary>
    public class DbFieldIgnoreRead : Attribute { }

    /// <summary>
    ///   A field flagged as Internal will be ignored for
    ///   all operations.
    /// </summary>
    public class DbFieldInternal : Attribute { }

    /// <summary>
    ///   A field flagged as OptionalFk will be ignored during
    ///   write operation if the value is 0 (default int value).
    ///   Read operation on OptionalFk field will return 0 if the database
    ///   value is NULL.
    /// </summary>
    public class DbFieldOptionalFk : Attribute { }


    /// <summary>
    ///   A field flagged as DbFieldEnumTypeCast will be converted
    ///   to the given type before writing it or after reading it.
    ///   DbTypeMatch MUST be a type that Npgsql can translate to SQL.
    /// </summary>
    public class DbFieldEnumTypeCast : Attribute
    {
        public Type DbType { get; set; }

        public DbFieldEnumTypeCast(Type t)
        {
            this.DbType = t;
        }
    }


    /// <summary>
    ///   Crud related high level functionalities.
    /// </summary>
    public static class Crud
    {
        /// <summary>
        ///   Reads one or more rows from the given query.
        /// </summary>
        /// <typeparam name="T">
        ///   Type of the instance to be created.
        ///   Must declare a parameterless constructor.
        /// </typeparam>
        /// <param name="fields">
        ///   Fields to be selected in the query.
        ///   If null, the fields are selected based on the property attributes.
        /// </param>
        /// <param name="conn">
        ///   Postgresql connection. If not null, the given connection is used.
        ///   If null, a new connection from the environment variable is open.
        /// </param>
        /// <returns> (Db associated error code, List of T on success (null otherwise)) </returns>
        public async static Task<(NpgsqlCrudErrno, List<T>)>
            Read<T>(string[] selectfields = null, WhereBuilder wb = null, NpgsqlConnection conn = null)
            where T : class, new()
        {
            bool connWasNull = false;

            if (conn == null)
            {
                connWasNull = true;
                conn = await Adapter.OpenFromEnv();
            }

            string query = QueryBuilder.QueryRead<T>(wb, selectfields);

            // Need to log in a dedicated file all the database queries...!
            await Adapter.LogToFile($"READ : {query}");
            var res = new List<T>();

            await using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
            {
                try
                {
                    await using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        res = await Adapter.ToObjectsAsync<T>(reader);
                    }
                }
                catch (PostgresException e)
                {
                    await Adapter.LogToFile(e);
                    Console.WriteLine(e);
                    return (NpgsqlCrudErrno.Generic, null);
                }
                finally
                {
                    if (connWasNull) conn.Dispose();
                }
            }

            return (NpgsqlCrudErrno.Ok, res);
        }


        /// <summary>
        ///   Reads one or more rows from the given query.
        /// </summary>
        /// <typeparam name="T">
        ///   Type of the instance to be created.
        ///   Must declare a parameterless constructor.
        /// </typeparam>
        /// <param name="selectfields">
        ///   Fields to be selected in the query.
        ///   If null, the fields are selected based on the property attributes.
        /// </param>
        /// <param name="conn">
        ///   Postgresql connection. If not null, the given connection is used.
        ///   If null, a new connection from the environment variable is open.
        /// </param>
        /// <returns> (Db associated error code, List of T on success (null otherwise)) </returns>
        public async static Task<(NpgsqlCrudErrno, T)>
            ReadFirst<T>(string[] selectfields = null, WhereBuilder wb = null, NpgsqlConnection conn = null)
            where T : class, new()
        {
            bool connWasNull = false;

            if (conn == null)
            {
                connWasNull = true;
                conn = await Adapter.OpenFromEnv();
            }

            string query = QueryBuilder.QueryRead<T>(wb, selectfields);

            // Need to log in a dedicated file all the database queries...!
            await Adapter.LogToFile($"READFIRST : {query}");

            T t = new();
            await using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
            {
                try
                {
                    await using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        t = await Adapter.ToObjectAsync<T>(reader);
                    }
                }
                catch (PostgresException e)
                {
                    Console.WriteLine(e);
                    await Adapter.LogToFile(e);
                    return (NpgsqlCrudErrno.Generic, null);
                }
                catch(Exception e)
                {
                    Console.WriteLine(e);
                    await Adapter.LogToFile(e);
                    return (NpgsqlCrudErrno.Generic, null);
                }
                finally
                {
                    if (connWasNull) conn.Dispose();
                }
            }

            return (NpgsqlCrudErrno.Ok, t);
        }


        /// <summary>
        ///   Reads the first row for the given id.
        /// </summary>
        /// <typeparam name="T">
        ///   Type of the instance to be created.
        ///   Must declare a parameterless constructor.
        /// </typeparam>
        /// <param name="fields">
        ///   Fields to be selected in the query.
        ///   If null, the fields are selected based on the property attributes.
        /// </param>
        /// <param name="conn">
        ///   Postgresql connection. If not null, the given connection is used.
        ///   If null, a new connection from the environment variable is open.
        /// </param>
        /// <returns> (Db associated error code, List of T on success (null otherwise)) </returns>
        public async static Task<(NpgsqlCrudErrno, T)>
            ReadFirst<T>(long id, string[] selectfields = null, NpgsqlConnection conn = null)
            where T : class, new()
        {
            bool connWasNull = false;

            if (conn == null)
            {
                connWasNull = true;
                conn = await Adapter.OpenFromEnv();
            }

            WhereBuilder wb = new();
            wb.AddClause(("id", id));
            string query = QueryBuilder.QueryRead<T>(wb, selectfields);

            await Adapter.LogToFile($"READFIRST ID : {query}");

            T t = new();
            await using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
            {
                try
                {
                    await using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        t = await Adapter.ToObjectAsync<T>(reader);
                    }
                }
                catch (PostgresException e)
                {
                    Console.WriteLine(e);
                    await Adapter.LogToFile(e);
                    return (NpgsqlCrudErrno.Generic, null);
                }
                finally
                {
                    if (connWasNull) conn.Dispose();
                }
            }

            return (NpgsqlCrudErrno.Ok, t);
        }



        /// <summary>
        ///   Performs an insert query from the given fields or object.
        /// </summary>
        /// <typeparam name="T">
        ///   Type to be converted from / to table columns (and match the table's name).
        /// </typeparam>
        /// <param name="t">
        ///   Object from where fields value are retrieved to fill the database row.
        /// </param>
        /// <param name="fields">
        ///   Fields to be inserted in the query.
        ///   If null, the fields are selected based on t properties.
        /// </param>
        /// <param name="conn">
        ///   Postgresql connection. If not null, the given connection is used.
        ///   If null, a new connection from the environment variable is open.
        /// </param>
        /// <returns> (Db associated error code, newly inserted id) </returns>
        public async static Task<(NpgsqlCrudErrno, long)>
            Create<T>(T t, string[] fields = null, NpgsqlConnection conn = null)
            where T : class, new()
        {
            bool connWasNull = false;

            if (conn == null)
            {
                connWasNull = true;
                conn = await Adapter.OpenFromEnv();
            }

            string query = QueryBuilder.QueryCreate<T>(t, fields);
            await Adapter.LogToFile($"CREATE : {query}");
            
            long insertedId;
            await using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
            {
                try
                {
                    insertedId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                }
                catch (PostgresException e)
                {
                    await Adapter.LogToFile($"CREATE EXCEPTION: {e.Message} {e.SqlState}");
                    Console.WriteLine($"CREATE EXCEPTION: {e.Message} {e.SqlState}");
                    if (e.SqlState == "23505")
                    {
                        return (NpgsqlCrudErrno.DuplicateKey, 0);
                    }
                    else
                    {
                        return (NpgsqlCrudErrno.Generic, 0);
                    }
                }
                finally
                {
                    if (connWasNull) conn.Dispose();
                }
            }

            return (NpgsqlCrudErrno.Ok, insertedId);
        }


        /// <summary>
        ///   Performs an insert query for each object in List<T>.
        /// </summary>
        /// <typeparam name="T">
        ///   Type to be converted from / to table columns (and match the table's name).
        /// </typeparam>
        /// <param name="ts">
        ///   Objects from where fields value are retrieved to fill the database row.
        /// </param>
        /// <param name="fields">
        ///   Fields to be inserted in the query.
        ///   If null, the fields are selected based on t properties.
        /// </param>
        /// <param name="conn">
        ///   Postgresql connection. If not null, the given connection is used.
        ///   If null, a new connection from the environment variable is open.
        /// </param>
        /// <returns> (Db associated error code, list of newly inserted ids) </returns>
        public async static Task<(NpgsqlCrudErrno, List<long>)>
            Create<T>(List<T> ts, string[] fields = null, NpgsqlConnection conn = null)
            where T : class, new()
        {
            bool connWasNull = false;

            if (conn == null)
            {
                connWasNull = true;
                conn = await Adapter.OpenFromEnv();
            }

            List<long> ids = new();
            NpgsqlTransaction tr = conn.BeginTransaction();

            foreach (T t in ts)
            {
                string query = QueryBuilder.QueryCreate<T>(t, fields);
                await Adapter.LogToFile($"CREATE : {query}");

                long insertedId;
                await using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
                {
                    try
                    {
                        insertedId = Convert.ToInt64(await cmd.ExecuteScalarAsync());
                        ids.Add(insertedId);
                    }
                    catch (PostgresException e)
                    {
                        Console.WriteLine($"CREATE EXCEPTION: {e.Message} {e.SqlState}");
                        await Adapter.LogToFile($"CREATE EXCEPTION: {e.Message} {e.SqlState}");
                        if (e.SqlState == "23505")
                        {
                            await tr.RollbackAsync();
                            if (connWasNull) conn.Dispose();
                            return (NpgsqlCrudErrno.DuplicateKey, null);
                        }
                        else
                        {
                            await tr.RollbackAsync();
                            if (connWasNull) conn.Dispose();
                            return (NpgsqlCrudErrno.Generic, null);
                        }
                    }
                }
            }

            await tr.CommitAsync();
            if (connWasNull) conn.Dispose();
            return (NpgsqlCrudErrno.Ok, ids);
        }


        /// <summary>
        ///   Performs an udpate query based on the id column.
        /// </summary>
        /// <typeparam name="T"> Type to be converted from / to table columns. </typeparam>
        /// <param name="id"> Id of the row to be updated. </param>
        /// <param name="t"> Object from where fields value are retrieved to fill the database row. </param>
        /// <param name="fields">
        ///   Fields to be inserted in the query.
        ///   If null, the fields are selected based on t properties.
        /// </param>
        /// <param name="conn">
        ///   Postgresql connection. If not null, the given connection is used.
        ///   If null, a new connection from the environment variable is open.
        /// </param>
        /// <returns> (Db associated error code, true if the row was updated (false otherwise)) </returns>
        public async static Task<(NpgsqlCrudErrno, bool)>
            Update<T>(long id, T t, string[] fields = null, NpgsqlConnection conn = null)
            where T : class, new()
        {
            bool connWasNull = false;

            if (conn == null)
            {
                connWasNull = true;
                conn = await Adapter.OpenFromEnv();
            }

            string query = QueryBuilder.QueryUpdate<T>(id, t, fields);
            await Adapter.LogToFile($"UPDATE : {query}");

            int affectedRows;
            await using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
            {
                try
                {
                    affectedRows = await cmd.ExecuteNonQueryAsync();
                }
                catch (PostgresException e)
                {
                    Console.WriteLine(e);
                    await Adapter.LogToFile(e);
                    return (NpgsqlCrudErrno.Generic, false);
                }
                finally
                {
                    if (connWasNull) conn.Dispose();
                }
            }

            return (NpgsqlCrudErrno.Ok, affectedRows == 1);
        }


        /// <summary>
        ///   Performs an udpate query based on the given where clause.
        /// </summary>
        /// <typeparam name="T"> Type to be converted from / to table columns. </typeparam>
        /// <param name="wb"> WhereBuilder to select the row to be updated. </param>
        /// <param name="t"> Object from where fields value are retrieved to fill the database row. </param>
        /// <param name="fields">
        ///   Fields to be inserted in the query.
        ///   If null, the fields are selected based on t properties.
        /// </param>
        /// <param name="conn">
        ///   Postgresql connection. If not null, the given connection is used.
        ///   If null, a new connection from the environment variable is open.
        /// </param>
        /// <returns> (Db associated error code, true if the row was updated (false otherwise)) </returns>
        public async static Task<(NpgsqlCrudErrno, bool)>
            Update<T>(WhereBuilder wb, T t, string[] fields = null, NpgsqlConnection conn = null)
            where T : class, new()
        {
            if (wb == null)
            {
                throw new Exception("Update with WB must not be called without at least" +
                    " one clause.");
            }

            bool connWasNull = false;

            if (conn == null)
            {
                connWasNull = true;
                conn = await Adapter.OpenFromEnv();
            }

            string query = QueryBuilder.QueryUpdate<T>(wb, t, fields);
            await Adapter.LogToFile($"UPDATE : {query}");

            int affectedRows;
            await using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
            {
                try
                {
                    affectedRows = await cmd.ExecuteNonQueryAsync();
                }
                catch (PostgresException e)
                {
                    Console.WriteLine(e);
                    await Adapter.LogToFile(e);
                    return (NpgsqlCrudErrno.Generic, false);
                }
                finally
                {
                    if (connWasNull) conn.Dispose();
                }
            }

            return (NpgsqlCrudErrno.Ok, affectedRows > 0);
        }




        /// <summary>
        ///   Performs an delete query based on the given id.
        /// </summary>
        /// <typeparam name="T">
        ///   Type to be converted from / to table columns (and match the table's name).
        /// </typeparam>
        /// <param name="id"> Id of the row to be deleted. </param>
        /// <param name="conn">
        ///   Postgresql connection. If not null, the given connection is used.
        ///   If null, a new connection from the environment variable is open.
        /// </param>
        /// <returns> (Db associated error code, true if the row was deleted (false otherwise)) </returns>
        public async static Task<(NpgsqlCrudErrno, bool)>
            Delete<T>(long id, NpgsqlConnection conn = null)
            where T : class, new()
        {
            bool connWasNull = false;

            if (conn == null)
            {
                connWasNull = true;
                conn = await Adapter.OpenFromEnv();
            }

            string query = QueryBuilder.QueryDelete<T>(id);
            await Adapter.LogToFile($"DELETE: {query}");

            int affectedRows;
            await using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
            {
                try
                {
                    affectedRows = await cmd.ExecuteNonQueryAsync();
                }
                catch (PostgresException e)
                {
                    await Adapter.LogToFile(e);
                    return (NpgsqlCrudErrno.Generic, false);
                }
                finally
                {
                    if (connWasNull) conn.Dispose();
                }
            }

            return (NpgsqlCrudErrno.Ok, affectedRows == 1);
        }

        /// <summary>
        ///   Performs an delete query based on the where clause.
        /// </summary>
        /// <typeparam name="T">
        ///   Type to be converted from / to table columns (and match the table's name).
        /// </typeparam>
        /// <param name="wb"></param>
        /// <param name="force"> Must be set to True if no where clauses specified. </param>
        /// <param name="conn">
        ///   Postgresql connection. If not null, the given connection is used.
        ///   If null, a new connection from the environment variable is open.
        /// </param>
        /// <returns> (Db associated error code, number of affected rows) </returns>
        public async static Task<(NpgsqlCrudErrno, int)>
            Delete<T>(WhereBuilder wb, bool force = false, NpgsqlConnection conn = null)
            where T : class, new()
        {
            bool connWasNull = false;

            if (conn == null)
            {
                connWasNull = true;
                conn = await Adapter.OpenFromEnv();
            }

            string query = QueryBuilder.QueryDelete<T>(wb, force);
            await Adapter.LogToFile($"DELETE: {query}");

            int affectedRows;
            await using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
            {
                try
                {
                    affectedRows = await cmd.ExecuteNonQueryAsync();
                }
                catch (PostgresException e)
                {
                    Console.WriteLine(e);
                    await Adapter.LogToFile(e);
                    return (NpgsqlCrudErrno.Generic, 0);
                }
                finally
                {
                    if (connWasNull) conn.Dispose();
                }
            }

            return (NpgsqlCrudErrno.Ok, affectedRows);
        }


        public async static Task<long>
            Count<T>(WhereBuilder wb, NpgsqlConnection conn = null)
            where T : class, new()
        {
            bool connWasNull = false;

            if (conn == null)
            {
                connWasNull = true;
                conn = await Adapter.OpenFromEnv();
            }

            string query = QueryBuilder.QueryCount<T>(wb);

            // Need to log in a dedicated file all the database queries...!
            await Adapter.LogToFile($"COUNT : {query}");

            long c = -1;
            await using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
            {
                try
                {
                    c = (long)await cmd.ExecuteScalarAsync();
                }
                catch (PostgresException e)
                {
                    await Adapter.LogToFile(e);
                    Console.WriteLine(e);
                    return -1;
                }
                finally
                {
                    if (connWasNull) conn.Dispose();
                }
            }

            return c;
        }




        /// <summary>
        ///   Raw query with no SELECT returned, only the number of affected rows.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="conn"></param>
        /// <returns></returns>
        public async static Task<(NpgsqlCrudErrno, int)>
        RawQuery(string query, NpgsqlConnection conn = null)
        {
            bool connWasNull = false;

            if (conn == null)
            {
                connWasNull = true;
                conn = await Adapter.OpenFromEnv();
            }

            await Adapter.LogToFile($"RAW : {query}");

            int nr;
            await using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
            {
                try
                {
                    nr = await cmd.ExecuteNonQueryAsync();
                }
                catch (PostgresException e)
                {
                    Console.WriteLine(e);
                    await Adapter.LogToFile(e);
                    return (NpgsqlCrudErrno.Generic, -1);
                }
                finally
                {
                    if (connWasNull) conn.Dispose();
                }
            }

            return (NpgsqlCrudErrno.Ok, nr);
        }



        /// <summary>
        ///   Raw query based on SELECT.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="query"></param>
        /// <param name="conn"></param>
        /// <returns></returns>
        public async static Task<(NpgsqlCrudErrno, List<T>)>
            RawQuery<T>(string query, NpgsqlConnection conn = null)
            where T : class, new()
        {
            bool connWasNull = false;

            if (conn == null)
            {
                connWasNull = true;
                conn = await Adapter.OpenFromEnv();
            }

            await Adapter.LogToFile($"RAW : {query}");
            var res = new List<T>();

            await using (NpgsqlCommand cmd = new NpgsqlCommand(query, conn))
            {
                try
                {
                    await using (NpgsqlDataReader reader = await cmd.ExecuteReaderAsync())
                    {
                        res = await Adapter.ToObjectsAsync<T>(reader);
                    }
                }
                catch (PostgresException e)
                {
                    Console.WriteLine(e);
                    await Adapter.LogToFile(e);
                    return (NpgsqlCrudErrno.Generic, null);
                }
                finally
                {
                    if (connWasNull) conn.Dispose();
                }
            }

            return (NpgsqlCrudErrno.Ok, res);
        }

    }

}
