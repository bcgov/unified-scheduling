using System;
using System.Collections.Generic;
using System.Linq;

namespace Unified.Common.Helpers.Extensions;

public static class ListExtensions
{
    public static List<T2> SelectToList<T, T2>(this IEnumerable<T> target, Func<T, T2> lambda)
    {
        return target.Select(lambda).ToList();
    }

    public static List<T2> SelectDistinctToList<T, T2>(this IEnumerable<T> target, Func<T, T2> lambda)
    {
        return target.Select(lambda).Distinct().ToList();
    }

    public static List<T> WhereToList<T>(this IEnumerable<T> target, Func<T, bool> lambda)
    {
        return target.Where(lambda).ToList();
    }
}
