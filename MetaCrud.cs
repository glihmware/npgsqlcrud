using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Npgsql;

namespace NpgsqlCrud
{
    /// <summary>
    ///   Some handy stuff generalized for queries.
    /// </summary>
    public static class MetaCrud
    {

        /// <summary>
        ///   Retrieves an entity from a unique field comparison.
        ///   (guid, label for instance)
        ///   
        ///   FIXME: deport into NpgsqlCrud...!
        /// </summary>
        /// <param name="what"></param>
        /// <param name="value"></param>
        /// <param name="conn"></param>
        /// <returns></returns>
        public static async Task<E>
        FromField<E>(string what, string value, NpgsqlConnection conn = null)
            where E : class, new()
        {
            WhereBuilder wb = new();
            wb.AddClause((what, value));

            (NpgsqlCrudErrno err, E e) = await Crud.ReadFirst<E>(wb: wb, conn: conn);
            if (err != NpgsqlCrudErrno.Ok) return null;

            return e;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="E"></typeparam>
        /// <param name="id"></param>
        /// <param name="conn"></param>
        /// <returns></returns>
        public static async Task<E>
        FromId<E>(int id, NpgsqlConnection conn = null)
            where E : class, new()
        {
            (NpgsqlCrudErrno err, E e) = await Crud.ReadFirst<E>(id, conn: conn);
            if (err != NpgsqlCrudErrno.Ok) return null;

            return e;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="E"></typeparam>
        /// <param name="id"></param>
        /// <param name="conn"></param>
        /// <returns></returns>
        public static async Task<E>
        FromId<E>(long id, NpgsqlConnection conn = null)
            where E : class, new()
        {
            (NpgsqlCrudErrno err, E e) = await Crud.ReadFirst<E>(id, conn: conn);
            if (err != NpgsqlCrudErrno.Ok) return null;

            return e;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="E"></typeparam>
        /// <param name="conn"></param>
        /// <returns></returns>
        public static async Task<List<E>>
        ListAll<E>(NpgsqlConnection conn = null)
            where E : class, new()
        {
            (NpgsqlCrudErrno err, List<E> e) = await Crud.Read<E>(conn: conn);
            if (err != NpgsqlCrudErrno.Ok) return null;

            return e;
        }


    }
}