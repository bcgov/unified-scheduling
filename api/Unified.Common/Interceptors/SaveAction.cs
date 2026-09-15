namespace Unified.Common.PostSave;

/// <summary>
/// The saved operation, mapped from EF's Added, Modified, or Deleted entity state.
/// </summary>
public enum SaveAction
{
    Create,
    Update,
    Delete,
}