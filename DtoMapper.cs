using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;

namespace NpgsqlCrud
{
    /// <summary>
    ///   Very basic mapper for dtos.
    ///   
    ///   WARNING: due to the use of reflexivity, some error
    ///   are only cought at RUNTIME.
    ///   Please consider implementing FromEntity(E e) and FromEntities(List<E> es)
    ///   for compile time checkings.
    /// </summary>
    public static class DtoMapper
    {


        /// <summary>
        ///   Maps entity fields into the given dto.
        /// </summary>
        /// <typeparam name="E"></typeparam>
        /// <typeparam name="D"></typeparam>
        /// <param name="e"></param>
        /// <returns> Null if entity was null, the dto otherwise. </returns>
        public static D EntityToDto<E, D>(E e)
            where E : class
            where D : class, new()
        {
            if (e == null)
            {
                return null;
            }

            D dto = new();

            foreach (PropertyInfo prop in dto.GetType().GetProperties())
            {
                PropertyInfo pe = e.GetType().GetProperty(prop.Name);
                if (pe == null)
                {
                    throw new Exception($"Entity {e.GetType()} is expected to have a property {prop.Name}. (Asked by dto {dto.GetType()}).");
                }

                prop.SetValue(dto, pe.GetValue(e));
            }

            return dto;
        }


        /// <summary>
        ///   Applies EntityToDto on an enumerable.
        /// </summary>
        /// <typeparam name="E"></typeparam>
        /// <typeparam name="D"></typeparam>
        /// <param name="es"></param>
        /// <returns> Null if the IEnumerable was null, the list of dto otherwise. </returns>
        public static List<D> EntitiesToDto<E, D>(IEnumerable<E> es)
            where E : class
            where D : class, new()
        {
            if (es == null)
            {
                return null;
            }

            List<D> dtos = new();

            foreach (E e in es)
            {
                dtos.Add(EntityToDto<E, D>(e));
            }

            return dtos;
        }
    }
}