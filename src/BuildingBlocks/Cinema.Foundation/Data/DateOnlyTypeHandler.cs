using System;
using System.Data;
using Dapper;

namespace Cinema.Foundation.Data;

public class DateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.ToDateTime(TimeOnly.MinValue);
    }

    public override DateOnly Parse(object value)
    {
        if (value is DateOnly d)
            return d;
        if (value is DateTime dt)
            return DateOnly.FromDateTime(dt);
        if (value is string s && DateOnly.TryParse(s, out var parsed))
            return parsed;
        return default;
    }
}

public class NullableDateOnlyTypeHandler : SqlMapper.TypeHandler<DateOnly?>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly? value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value.HasValue ? value.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
    }

    public override DateOnly? Parse(object value)
    {
        if (value == null || value is DBNull)
            return null;
        if (value is DateOnly d)
            return d;
        if (value is DateTime dt)
            return DateOnly.FromDateTime(dt);
        if (value is string s && DateOnly.TryParse(s, out var parsed))
            return parsed;
        return null;
    }
}
