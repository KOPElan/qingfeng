namespace QingFeng.Models;

/// <summary>
/// Type of share (CIFS/Samba or NFS)
/// </summary>
public enum ShareType
{
    CIFS,
    NFS
}

/// <summary>
/// Represents a shared directory configuration
/// </summary>
public class ShareInfo
{
    /// <summary>
    /// Share name (for CIFS) or export identifier
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Local directory path being shared
    /// </summary>
    public string Path { get; set; } = string.Empty;
    
    /// <summary>
    /// Type of share (CIFS or NFS)
    /// </summary>
    public ShareType Type { get; set; }
    
    /// <summary>
    /// Description/comment for the share
    /// </summary>
    public string Comment { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether the share is browseable (CIFS only)
    /// </summary>
    public bool Browseable { get; set; } = true;
    
    /// <summary>
    /// Whether the share is read-only
    /// </summary>
    public bool ReadOnly { get; set; } = false;
    
    /// <summary>
    /// Whether guest access is allowed (CIFS only)
    /// </summary>
    public bool GuestOk { get; set; } = false;
    
    /// <summary>
    /// Valid users list (CIFS only)
    /// </summary>
    public string ValidUsers { get; set; } = string.Empty;
    
    /// <summary>
    /// Write list - users who can write (CIFS only)
    /// </summary>
    public string WriteList { get; set; } = string.Empty;
    
    /// <summary>
    /// Client hosts allowed to access (NFS only)
    /// </summary>
    public string AllowedHosts { get; set; } = "*";
    
    /// <summary>
    /// NFS export options (e.g., rw, sync, no_subtree_check)
    /// </summary>
    public string NfsOptions { get; set; } = "rw,sync,no_subtree_check";
    
    /// <summary>
    /// Additional custom options as raw configuration text
    /// </summary>
    public Dictionary<string, string> CustomOptions { get; set; } = new();
}

/// <summary>
/// Request model for creating/updating a share
/// </summary>
public class ShareRequest
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public ShareType Type { get; set; }
    public string Comment { get; set; } = string.Empty;
    public bool Browseable { get; set; } = true;
    public bool ReadOnly { get; set; } = false;
    public bool GuestOk { get; set; } = false;
    public string ValidUsers { get; set; } = string.Empty;
    public string WriteList { get; set; } = string.Empty;
    public string AllowedHosts { get; set; } = "*";
    public string NfsOptions { get; set; } = "rw,sync,no_subtree_check";
}

/// <summary>
/// Represents a Samba user
/// </summary>
public class SambaUser
{
    /// <summary>
    /// Unix username
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Unix user ID
    /// </summary>
    public int Uid { get; set; }
    
    /// <summary>
    /// Whether the user has a Samba password set
    /// </summary>
    public bool HasSambaPassword { get; set; }
}

/// <summary>
/// Request model for creating/updating a Samba user
/// </summary>
public class SambaUserRequest
{
    /// <summary>
    /// Unix username (must exist on the system)
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Samba password for the user
    /// </summary>
    public string Password { get; set; } = string.Empty;
}
