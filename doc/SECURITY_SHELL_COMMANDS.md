# Security Considerations for Shell Command Tasks

## ⚠️ CRITICAL SECURITY WARNINGS

This document outlines important security considerations for the Shell Command task feature in the QingFeng scheduled tasks system.

## Overview

The Shell Command task type allows executing arbitrary shell commands as scheduled or one-time background tasks. While this provides great flexibility, **it also introduces significant security risks if not properly managed**.

## Risk Assessment

### HIGH RISK: Command Injection

**Severity**: CRITICAL  
**Description**: Commands are executed via `/bin/bash -c` without sanitization. Malicious commands can:
- Access or modify any files the application has permissions to
- Execute system commands
- Potentially compromise the entire system if the application runs with elevated privileges

**Mitigation**:
1. **Restrict Access**: Only allow trusted administrators to create shell command tasks
2. **Input Validation**: Never accept user input directly in command strings
3. **Command Allowlist**: Consider implementing an allowlist of permitted commands
4. **Code Review**: Review all shell command tasks before enabling them
5. **Audit Logging**: Log all task creation and execution events

### MEDIUM RISK: Path Traversal

**Severity**: MEDIUM  
**Description**: Working directory paths could potentially be used to access unauthorized areas of the filesystem.

**Mitigation** (Implemented):
- Working directories are validated using `FileManagerService.IsPathAllowed()`
- Only pre-approved paths can be used
- Non-existent paths cause task failure

### MEDIUM RISK: Resource Exhaustion

**Severity**: MEDIUM  
**Description**: Poorly written or malicious commands could consume excessive system resources.

**Mitigation** (Partially Implemented):
- Timeout mechanism prevents indefinite execution (configurable, max 24 hours)
- Tasks run as separate processes that can be terminated

**Future Mitigations Needed**:
- CPU usage limits
- Memory usage limits
- Concurrent task execution limits

### LOW RISK: Information Disclosure

**Severity**: LOW-MEDIUM  
**Description**: Command strings and captured output are stored in the database and logs.

**Mitigation**:
1. Never include sensitive data (passwords, API keys) in command strings
2. Use environment variables or secure configuration for secrets
3. Implement proper access controls on execution history
4. Consider encrypting sensitive execution logs

## Security Best Practices

### For Administrators

1. **Principle of Least Privilege**
   - Run the application with minimal necessary permissions
   - Never run as root/administrator unless absolutely necessary
   - Use dedicated service accounts with restricted permissions

2. **Access Control**
   - Implement authentication for the task management interface
   - Restrict task creation to administrator role only
   - Consider requiring additional approval for shell command tasks

3. **Command Review Process**
   ```
   - Administrator creates shell command task (disabled state)
   - Security review of command and configuration
   - Second administrator approves and enables task
   - Regular audits of active tasks
   ```

4. **Monitoring and Alerting**
   - Monitor task execution logs for suspicious activity
   - Set up alerts for failed tasks or unusual patterns
   - Regular review of execution history

5. **Sandboxing** (Recommended)
   - Consider running shell command tasks in containers
   - Use Linux namespaces or cgroups for resource isolation
   - Implement mandatory access control (SELinux, AppArmor)

### For Developers

1. **Never Trust User Input**
   ```csharp
   // BAD - Direct user input in command
   var command = $"rm -f {userInput}";
   
   // GOOD - Validate and sanitize
   if (!IsValidFilename(userInput)) 
       throw new SecurityException("Invalid filename");
   var command = $"rm -f /safe/path/{SanitizeFilename(userInput)}";
   ```

2. **Use Parameterized Commands** (When Possible)
   ```csharp
   // Prefer building commands programmatically
   var safeArgs = new[] { "-la", validatedPath };
   // Rather than: "ls -la " + userPath
   ```

3. **Implement Command Allowlist**
   ```csharp
   private static readonly HashSet<string> AllowedCommands = new()
   {
       "/usr/bin/backup-script.sh",
       "/usr/bin/cleanup-temp.sh",
       // etc.
   };
   
   if (!AllowedCommands.Contains(config.Command))
       throw new SecurityException("Command not in allowlist");
   ```

4. **Validate All Paths**
   - Always use `FileManagerService.IsPathAllowed()` for path validation
   - Reject relative paths in user input
   - Validate paths exist before execution

## Deployment Recommendations

### Production Environment

1. **Disable Shell Command Tasks** (if not needed)
   - If only FileIndexing tasks are needed, disable ShellCommand support
   - Modify UI to hide shell command option
   - Add application setting: `Features:AllowShellCommands: false`

2. **Network Isolation**
   - Limit network access for the application
   - Use firewall rules to restrict outbound connections
   - Monitor network traffic for suspicious activity

3. **Database Security**
   - Encrypt task configurations in database
   - Implement row-level security
   - Regular database backups with secure storage

4. **Compliance**
   - Ensure shell command usage complies with organizational policies
   - Document all shell command tasks for compliance audits
   - Implement retention policies for execution logs

## Incident Response

### If Suspicious Activity Detected

1. **Immediate Actions**
   ```
   - Disable all shell command tasks immediately
   - Stop the ScheduledTaskExecutorService
   - Review recent execution logs
   - Check system for unauthorized changes
   ```

2. **Investigation**
   ```
   - Identify who created suspicious tasks
   - Review all recently created/modified tasks
   - Check execution history for unusual patterns
   - Analyze captured command output
   ```

3. **Recovery**
   ```
   - Remove unauthorized tasks
   - Reset affected user credentials
   - Restore from known-good backup if needed
   - Implement additional security controls
   ```

## Code Review Checklist

Before approving shell command tasks in production:

- [ ] Command does not contain user input
- [ ] All paths are validated
- [ ] No hardcoded credentials or secrets
- [ ] Timeout is appropriate for the task
- [ ] Working directory is authorized
- [ ] Command follows least privilege principle
- [ ] Task creator is authorized administrator
- [ ] Execution schedule is reasonable
- [ ] Output capture is enabled for audit
- [ ] Task has business justification

## Future Security Enhancements

Recommended improvements for future versions:

1. **Command Allowlist**: Built-in allowlist configuration
2. **Approval Workflow**: Two-person rule for task creation
3. **Resource Limits**: CPU, memory, disk I/O limits
4. **Sandboxing**: Container or VM isolation
5. **Command Templates**: Pre-approved command templates
6. **Encryption**: Encrypt sensitive task configurations
7. **MFA**: Require multi-factor auth for shell command task creation
8. **Audit Log**: Comprehensive audit trail with tamper protection

## References

- OWASP Command Injection: https://owasp.org/www-community/attacks/Command_Injection
- CWE-78: OS Command Injection: https://cwe.mitre.org/data/definitions/78.html
- Linux Security Best Practices: https://www.kernel.org/doc/html/latest/admin-guide/security.html

## Contact

For security concerns or to report vulnerabilities, please contact the security team.

---

**Last Updated**: 2026-01-12  
**Version**: 1.0
