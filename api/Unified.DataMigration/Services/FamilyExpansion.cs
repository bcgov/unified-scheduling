namespace Unified.DataMigration.Services;

/// <summary>
/// Indexed form of the graph replay's family expansion. It reproduces the original pass loop
/// exactly, including the order in which components join the family and old members are found,
/// but finds the groups/components that touch a new key through lookups instead of rescanning
/// every old group and proposed component on every pass.
/// </summary>
/// <remarks>
/// Original algorithm, per pass until nothing expands:
/// (1) visit old groups in index order; a group whose member key is already known adds its
/// unseen members and their keys (keys added here make later groups eligible in this pass);
/// (2) visit components in index order; a component with a known key that is new to the family
/// joins it and adds its keys (likewise for later components in this pass).
/// Revisiting a processed group or family component has no effect, so each is processed once,
/// at the first visit after it became eligible.
/// </remarks>
internal sealed class FamilyExpansion<TMember>
    where TMember : notnull
{
    private readonly IReadOnlyList<IReadOnlyList<string>> components;
    private readonly IReadOnlyList<IReadOnlyList<TMember>> groups;
    private readonly Func<TMember, string> memberKey;
    private readonly Dictionary<string, List<int>> componentsByKey = [];
    private readonly Dictionary<string, List<int>> groupsByKey = [];

    public FamilyExpansion(
        IReadOnlyList<IReadOnlyList<string>> components,
        IReadOnlyList<IReadOnlyList<TMember>> groups,
        Func<TMember, string> memberKey
    )
    {
        this.components = components;
        this.groups = groups;
        this.memberKey = memberKey;
        for (var i = 0; i < components.Count; i++)
            foreach (var key in components[i])
                Index(componentsByKey, key, i);
        for (var i = 0; i < groups.Count; i++)
            foreach (var member in groups[i])
                Index(groupsByKey, memberKey(member), i);
    }

    private static void Index(Dictionary<string, List<int>> index, string key, int position)
    {
        if (!index.TryGetValue(key, out var list))
            index[key] = list = [];
        if (list.Count == 0 || list[^1] != position)
            list.Add(position);
    }

    public (List<int> Family, HashSet<string> Keys, List<TMember> OldMembers) Expand(int index)
    {
        var family = new List<int> { index };
        var inFamily = new HashSet<int> { index };
        var keys = new HashSet<string>();
        var oldMembers = new List<TMember>();
        var knownMembers = new HashSet<TMember>();
        var visitedGroups = new HashSet<int>();
        var groupsNow = new SortedSet<int>();
        var groupsNext = new SortedSet<int>();
        var componentsNow = new SortedSet<int>();
        var componentsNext = new SortedSet<int>();
        var inGroupPhase = true;
        var cursor = -1;

        // A newly known key makes its groups/components eligible: in the current phase when they
        // are still ahead of the cursor, otherwise at their phase in the next pass.
        bool AddKey(string key)
        {
            if (!keys.Add(key))
                return false;
            if (groupsByKey.TryGetValue(key, out var touchedGroups))
                foreach (var g in touchedGroups)
                    if (!visitedGroups.Contains(g))
                        (inGroupPhase && g > cursor ? groupsNow : groupsNext).Add(g);
            if (componentsByKey.TryGetValue(key, out var touchedComponents))
                foreach (var c in touchedComponents)
                    if (!inFamily.Contains(c))
                        (inGroupPhase || c > cursor ? componentsNow : componentsNext).Add(c);
            return true;
        }

        foreach (var key in components[index])
            AddKey(key);
        bool expanded;
        do
        {
            expanded = false;
            inGroupPhase = true;
            cursor = -1;
            while (groupsNow.Count > 0)
            {
                var g = groupsNow.Min;
                groupsNow.Remove(g);
                cursor = g;
                if (!visitedGroups.Add(g))
                    continue;
                foreach (var member in groups[g])
                {
                    if (knownMembers.Add(member))
                        oldMembers.Add(member);
                    if (AddKey(memberKey(member)))
                        expanded = true;
                }
            }
            inGroupPhase = false;
            cursor = -1;
            while (componentsNow.Count > 0)
            {
                var c = componentsNow.Min;
                componentsNow.Remove(c);
                cursor = c;
                if (!inFamily.Add(c))
                    continue;
                family.Add(c);
                foreach (var key in components[c])
                    AddKey(key);
                expanded = true;
            }
            (groupsNow, groupsNext) = (groupsNext, groupsNow);
            (componentsNow, componentsNext) = (componentsNext, componentsNow);
        } while (expanded);
        return (family, keys, oldMembers);
    }
}
