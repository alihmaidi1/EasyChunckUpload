
namespace EasyChunkUpload.ChunkExtension;
public class ChunkUploadSettings
{

    
    /// <summary>
    /// Contains the root path for temporary chunk storage during file upload operations
    /// </summary>
    /// <remarks>
    /// <para>This field is initialized from configuration with fallback to:</para>
    /// <code>Path.Combine(WebHostEnvironment.ContentRootPath, "App_Data/Chunks")</code>
    /// 
    /// <para><strong>Security Notice:</strong></para>
    /// <list type="bullet">
    /// <item>Should never be exposed through public endpoints</item>
    /// <item>Directory permissions should be restricted to application identity only</item>
    /// <item>Validated against path traversal attempts during initialization</item>
    /// </list>
    /// 
    /// <para><strong>File System Behavior:</strong></para>
    /// <list type="number">
    /// <item>Automatically created if non-existent</item>
    /// <item>Regularly cleaned by background service</item>
    /// <item>Uses isolated storage per upload session</item>
    /// </list>
    /// </remarks>
    /// <example>
    /// Typical resolved path structure:
    /// <code>
    /// {TempFolder}/
    /// ├── {session-id}/
    /// │   ├── chunk_1.bin
    /// │   ├── chunk_2.bin
    /// │   └── metadata.json
    /// </code>
    /// </example>
    public string TempFolder{get;set;}

/// <summary>
/// Gets or sets the interval in Seconds between automatic cleanup cycles
/// </summary>
/// <value>
/// <para>Integer value representing minutes between cleanup executions</para>
/// <para>Default: 60 minutes (1 hour)</para>
/// </value>
/// <remarks>
/// <para>Configured through appsettings.json using key:</para>
/// <code>"ChunkUpload": { "CleanupInterval": 30 }</code>
/// 
/// <para><strong>Behavior Notes:</strong></para>
/// <list type="bullet">
/// <item>Set to 0 to disable automatic cleanup (not recommended) This will down your memory</item>
/// </list>
/// 
/// <para><strong>Performance Considerations:</strong></para>
/// <list type="number">
/// <item>Frequent intervals (＜15 mins) may impact system performance</item>
/// <item>Infrequent intervals (＞360 mins) may accumulate stale data</item>
/// <item>Recommended range: 15-240 minutes</item>
/// </list>
/// 
/// <para>Actual cleanup timing may vary ±10% due to system scheduler variance</para>
/// </remarks>

    
    public int CleanupInterval { get; set; }=60*60;
    
    /// <summary>
    /// Gets or sets the retention period in days for completed files before automatic deletion
    /// </summary>
    /// <value>
    /// <para>Integer value representing days to retain completed files</para>
    /// <para>Valid range: 1-365 (1 day to 1 year)</para>
    /// <para>Default: 7 days</para>
    /// </value>
    /// <remarks>
    /// <para>Configured through appsettings.json using key:</para>
    /// <code>"ChunkUpload": { "CompletedFilesExpiration": 14 }</code>
    /// 
    /// <para><strong>Retention Rules:</strong></para>
    /// <list type="bullet">
    /// <item>Timer-based deletion runs with <see cref="CleanupInterval"/></item>
    /// <item>Expiration countdown starts when file status changes to Completed</item>
    /// <item>Files are permanently deleted - not moved to recycle bin</item>
    /// </list>
    /// 
    /// <para><strong>Value Handling:</strong></para>
    /// <list type="number">
    /// <item>Set to -1 to disable expiration (not recommended for production)</item>
    /// </list>
    /// 
    /// <para><strong>Compliance Notes:</strong></para>
    /// <list type="bullet">
    /// <item>Affects GDPR "right to erasure" compliance</item>
    /// <item>Consider legal retention requirements for your domain</item>
    /// <item>Audit logs are maintained separately from file storage</item>
    /// </list>
    /// </remarks>


    public int CompletedFilesExpiration { get; set; }=60*60*24*7;


}
