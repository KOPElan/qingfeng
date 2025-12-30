using QingFeng.Models;

namespace QingFeng.Services;

/// <summary>
/// Service for managing host shared directories (CIFS/Samba and NFS exports)
/// </summary>
public interface IShareManagementService
{
    /// <summary>
    /// Get all configured shares (both CIFS and NFS)
    /// </summary>
    Task<List<ShareInfo>> GetAllSharesAsync();
    
    /// <summary>
    /// Get all CIFS/Samba shares
    /// </summary>
    Task<List<ShareInfo>> GetCifsSharesAsync();
    
    /// <summary>
    /// Get all NFS exports
    /// </summary>
    Task<List<ShareInfo>> GetNfsSharesAsync();
    
    /// <summary>
    /// Get a specific share by name and type
    /// </summary>
    Task<ShareInfo?> GetShareAsync(string name, ShareType type);
    
    /// <summary>
    /// Add a new CIFS share
    /// </summary>
    Task<string> AddCifsShareAsync(ShareRequest request);
    
    /// <summary>
    /// Add a new NFS export
    /// </summary>
    Task<string> AddNfsShareAsync(ShareRequest request);
    
    /// <summary>
    /// Update an existing CIFS share
    /// </summary>
    Task<string> UpdateCifsShareAsync(string shareName, ShareRequest request);
    
    /// <summary>
    /// Update an existing NFS export
    /// </summary>
    Task<string> UpdateNfsShareAsync(string exportPath, ShareRequest request);
    
    /// <summary>
    /// Remove a CIFS share
    /// </summary>
    Task<string> RemoveCifsShareAsync(string shareName);
    
    /// <summary>
    /// Remove an NFS export
    /// </summary>
    Task<string> RemoveNfsShareAsync(string exportPath);
    
    /// <summary>
    /// Restart Samba service to apply configuration changes
    /// </summary>
    Task<string> RestartSambaServiceAsync();
    
    /// <summary>
    /// Restart NFS service to apply configuration changes
    /// </summary>
    Task<string> RestartNfsServiceAsync();
    
    /// <summary>
    /// Test Samba configuration for syntax errors
    /// </summary>
    Task<string> TestSambaConfigAsync();
    
    /// <summary>
    /// Detect available features and required packages
    /// </summary>
    Task<ShareManagementFeatureDetection> DetectFeaturesAsync();
    
    /// <summary>
    /// Get all Samba users
    /// </summary>
    Task<List<SambaUser>> GetSambaUsersAsync();
    
    /// <summary>
    /// Add a Samba user with password
    /// </summary>
    Task<OperationResult> AddSambaUserAsync(SambaUserRequest request);
    
    /// <summary>
    /// Update a Samba user's password
    /// </summary>
    Task<OperationResult> UpdateSambaUserPasswordAsync(string username, string password);
    
    /// <summary>
    /// Remove a Samba user
    /// </summary>
    Task<OperationResult> RemoveSambaUserAsync(string username);
}
