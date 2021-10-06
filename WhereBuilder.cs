using System;
using System.Collections.Generic;

namespace NpgsqlCrud
{
    public class WhereBuilder
    {
        string _s = " WHERE (";
        string _a = null;
        bool _closedEnd = false;

        public void After(string after)
        {
            _a = after;
        }


        public void And((string, object) fieldValue, string op = "=", bool scoped = true)
        {
            this.AddClause(fieldValue, op, "AND", scoped);
        }

        public void Or((string, object) fieldValue, string op = "=", bool scoped = true)
        {
            this.AddClause(fieldValue, op, "OR", scoped);
        }

        public void AndBetween((string, object, object) fields, bool scoped = true)
        {

            this.AddClause((fields.Item1, $"{fields.Item2}' AND '{fields.Item3}"), op: "BETWEEN", andOr: "AND");
        }


        public void AddClause(
            (string, object) fieldValue,
            string op = "=",
            string andOr = null,
            bool scoped = true)
        {
            string name = CaseFormatter.CamelCaseToLowerCase(fieldValue.Item1);

            string val;
            if (fieldValue.Item2 != null)
            {
                val = fieldValue.Item2.ToString();
                if (op != "BETWEEN") val = val.Replace("'", "''");
                if (op == "IN")
                {
                    var _i2 = fieldValue.Item2;
                    if (!(_i2 is IEnumerable<int>) && (_i2 is IEnumerable<string>))
                    {
                        throw new Exception("Operator IN is expecting IEnumerable<int|string> as value.");
                    }

                    val = string.Join(", ", _i2);
                }
            }
            else
            {
                val = "NULL";
            }

            if (!string.IsNullOrEmpty(andOr)) this._s += $" {andOr} ";

            if (scoped) this.Open();

            if (op.Contains("IN"))
            {
                this._s += $"{name} {op} {val}";
            }
            else
            {
                this._s += $"{name} {op} '{val}'";
            }

            if (scoped) this.Close();
        }

        public void Open()
        {
            this._s += "(";
        }

        public void Close()
        {
            this._s += ")";
        }

        public override string ToString()
        {
            if (!this._closedEnd)
            {
                this.Close();
                this._closedEnd = true;
            }

            if (string.IsNullOrEmpty(_a)) return _s;
            return $"{_s} {_a}";
        }

    }
}
