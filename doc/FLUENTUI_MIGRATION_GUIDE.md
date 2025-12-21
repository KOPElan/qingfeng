# FluentUI Migration Guide for Remaining Pages

## Overview
This guide provides step-by-step instructions for completing the FluentUI migration for the two remaining complex pages: DiskManagement.razor and FileManager.razor.

## Current Status
- **Completed**: 11/13 pages (84.6%)
- **Remaining**: 2 pages (DiskManagement.razor, FileManager.razor)
- **Build Status**: ✅ 0 errors, 1 pre-existing warning

## Page 1: DiskManagement.razor (944 lines)

### Overview
Complex disk management page with:
- 2 data tables (block devices, network disks)
- 3 modal wizards (Mount, Network Mount, Power Management)
- Multiple form controls and progress bars

### Migration Strategy

#### Step 1: Main Layout (Lines 1-40)
```razor
<!-- Replace -->
@if (!IsAuthorized) {
    <div class="loading-container">
        <div class="spinner-border text-primary">
```

<!-- With -->
@if (!IsAuthorized) {
    <FluentStack HorizontalAlignment="HorizontalAlignment.Center" 
                 VerticalAlignment="VerticalAlignment.Center" 
                 Style="min-height: 200px;">
        <FluentProgressRing />
    </FluentStack>
```

#### Step 2: Page Header and Buttons (Lines 20-31)
```razor
<!-- Replace -->
<h3 class="mb-4">磁盘管理</h3>
<div class="d-flex gap-2 mb-4">
    <button type="button" class="btn btn-custom-primary" @onclick="RefreshData">
        <i class="@Icons.Material.Filled.Refresh me-2"></i>
        刷新
    </button>

<!-- With -->
<FluentStack Orientation="Orientation.Vertical" Style="padding: 2rem;">
    <FluentLabel Typo="Typography.H3">💿 磁盘管理</FluentLabel>
    <FluentStack Orientation="Orientation.Horizontal" Style="gap: 0.5rem; margin-bottom: 1rem;">
        <FluentButton Appearance="Appearance.Accent" OnClick="RefreshData">
            🔄 刷新
        </FluentButton>
```

#### Step 3: Alert Messages (Lines 33-39)
```razor
<!-- Replace -->
<div class="alert @(isError ? "alert-danger" : "alert-success") mb-4">
    @message
    <button type="button" class="btn-close" @onclick="@(() => message = string.Empty)">
</div>

<!-- With -->
<FluentMessageBar Intent="@(isError ? MessageIntent.Error : MessageIntent.Success)" 
                  Style="margin-bottom: 1rem;">
    @message
    <FluentButton Appearance="Appearance.Lightweight" 
                  OnClick="@(() => message = string.Empty)" 
                  aria-label="Close" 
                  Style="margin-left: auto;">
        ✕
    </FluentButton>
</FluentMessageBar>
```

#### Step 4: Disk Tables (Lines 42-185, 187-260)
```razor
<!-- Replace Bootstrap table wrapper -->
<div class="card-custom p-4 mb-4">
    <h5 class="mb-3">
        <i class="@Icons.Material.Filled.Storage me-2"></i>
        所有磁盘设备
    </h5>
    <div class="table-responsive">
        <table class="table table-custom">

<!-- With FluentUI wrapper -->
<FluentCard Style="margin-bottom: 1.5rem;">
    <FluentLabel Typo="Typography.H5">
        💾 所有磁盘设备
    </FluentLabel>
    <div style="overflow-x: auto;">
        <table class="fluent-table" style="width: 100%; margin-top: 1rem;" 
               role="table" aria-label="磁盘设备列表">
```

```razor
<!-- Replace progress bars in table cells -->
<div class="progress-custom" style="height: 20px;">
    <div class="progress-bar-custom" style="width: @disk.UsagePercent%">
        <small>@disk.UsagePercent%</small>
    </div>
</div>

<!-- With -->
<div style="display: flex; align-items: center; gap: 0.5rem;">
    <FluentProgress Value="@((int)disk.UsagePercent)" Max="100" 
                    Style="flex: 1; min-width: 0;" />
    <small>@disk.UsagePercent%</small>
</div>
```

```razor
<!-- Replace badges -->
<span class="badge bg-info ms-1">可移动</span>

<!-- With -->
<FluentBadge Appearance="Appearance.Accent" 
             BackgroundColor="var(--info)" 
             Style="margin-left: 0.25rem;">可移动</FluentBadge>
```

```razor
<!-- Replace action buttons -->
<button type="button" class="btn btn-outline-danger btn-sm" 
        @onclick="() => UnmountDiskAction(disk.MountPoint)">
    <i class="@Icons.Material.Filled.Eject me-1"></i>
    卸载
</button>

<!-- With -->
<FluentButton Appearance="Appearance.Outline" 
              OnClick="() => UnmountDiskAction(disk.MountPoint)" 
              Title="卸载">
    ⏏️ 卸载
</FluentButton>
```

#### Step 5: Mount Wizard Modal (Lines 274-356) ⚠️ CRITICAL
This is the most complex part. Bootstrap modal must be completely replaced:

```razor
<!-- Old Bootstrap Modal Structure -->
@if (showMountWizard)
{
    <div class="modal show d-block" tabindex="-1">
        <div class="modal-dialog modal-lg">
            <div class="modal-content">
                <div class="modal-header">
                    <h5 class="modal-title">磁盘挂载向导</h5>
                    <button type="button" class="btn-close" @onclick="HideMountWizard"></button>
                </div>
                <div class="modal-body">
                    <!-- Form content -->
                </div>
                <div class="modal-footer">
                    <button class="btn btn-secondary" @onclick="HideMountWizard">取消</button>
                    <button class="btn btn-custom-primary" @onclick="ExecuteMountWizard">挂载</button>
                </div>
            </div>
        </div>
    </div>
    <div class="modal-backdrop show"></div>
}

<!-- New FluentUI Dialog Structure -->
<FluentDialog Hidden="@(!showMountWizard)" 
              Modal="true" 
              OnDismiss="HideMountWizard" 
              Style="min-width: 600px;">
    <FluentDialogHeader>
        <FluentLabel Typo="Typography.H5">➕ 磁盘挂载向导</FluentLabel>
    </FluentDialogHeader>
    <FluentDialogBody>
        @if (!string.IsNullOrEmpty(wizardMessage))
        {
            <FluentMessageBar Intent="@(wizardError ? MessageIntent.Error : MessageIntent.Success)" 
                              Style="margin-bottom: 1rem;">
                @wizardMessage
                <FluentButton Appearance="Appearance.Lightweight" 
                              OnClick="@(() => wizardMessage = string.Empty)" 
                              Style="margin-left: auto;">✕</FluentButton>
            </FluentMessageBar>
        }
        
        <FluentStack Orientation="Orientation.Vertical" Style="gap: 1rem;">
            <FluentTextField Label="设备路径 *" 
                           @bind-Value="wizardDevicePath" 
                           Placeholder="/dev/sdb1" 
                           Style="width: 100%;">
                <FluentLabel Slot="helper-text" Style="font-size: 0.875rem;">
                    例如: /dev/sdb1
                </FluentLabel>
            </FluentTextField>
            
            <FluentTextField Label="挂载点 *" 
                           @bind-Value="wizardMountPoint" 
                           Placeholder="/mnt/mydisk" 
                           Style="width: 100%;">
                <FluentLabel Slot="helper-text" Style="font-size: 0.875rem;">
                    例如: /mnt/mydisk
                </FluentLabel>
            </FluentTextField>
            
            <FluentSelect Label="文件系统类型（可选）" 
                         @bind-Value="wizardFileSystem" 
                         Style="width: 100%;">
                <FluentOption Value="">自动检测</FluentOption>
                @foreach (var fs in availableFileSystems)
                {
                    <FluentOption Value="@fs">@fs</FluentOption>
                }
            </FluentSelect>
            
            <FluentTextField Label="挂载选项（可选）" 
                           @bind-Value="wizardOptions" 
                           Placeholder="defaults" 
                           Style="width: 100%;">
                <FluentLabel Slot="helper-text" Style="font-size: 0.875rem;">
                    例如: defaults, rw, noatime
                </FluentLabel>
            </FluentTextField>
            
            <!-- Radio buttons can stay as HTML or use FluentRadioGroup -->
            <FluentStack Orientation="Orientation.Vertical">
                <FluentLabel>挂载类型 *</FluentLabel>
                <FluentRadio Name="mountType" 
                           Value="temp" 
                           @bind-Checked="@tempMountChecked">
                    临时挂载（仅当前会话，重启后失效）
                </FluentRadio>
                <FluentRadio Name="mountType" 
                           Value="perm" 
                           @bind-Checked="@permMountChecked">
                    永久挂载（写入 /etc/fstab，重启后自动挂载）
                </FluentRadio>
            </FluentStack>
        </FluentStack>
    </FluentDialogBody>
    <FluentDialogFooter>
        <FluentButton Appearance="Appearance.Neutral" OnClick="HideMountWizard">
            取消
        </FluentButton>
        <FluentButton Appearance="Appearance.Accent" 
                     OnClick="ExecuteMountWizard" 
                     Disabled="@(string.IsNullOrWhiteSpace(wizardDevicePath) || 
                                 string.IsNullOrWhiteSpace(wizardMountPoint) || 
                                 isProcessing)">
            @if (isProcessing)
            {
                <FluentProgressRing Style="width: 16px; height: 16px; margin-right: 0.5rem;" />
            }
            ✓ 挂载
        </FluentButton>
    </FluentDialogFooter>
</FluentDialog>
```

**Important Notes for Radio Buttons:**
- If using FluentRadio, you need to manage the state differently than Bootstrap radio inputs
- Consider adding bool properties: `tempMountChecked` and `permMountChecked`
- Or keep simple HTML radio inputs if FluentRadio causes complexity

#### Step 6: Power Management Modal (Lines 358-464)
Follow the same pattern as Mount Wizard Modal. Replace:
- Modal structure → FluentDialog
- Form controls → FluentUI components
- Buttons → FluentButton

#### Step 7: Network Mount Wizard Modal (Lines 466-end)
Follow the same pattern. This modal has network-specific forms.

### Testing Checklist for DiskManagement.razor
After each step, build and verify:
- [ ] Page loads without errors
- [ ] Tables display correctly
- [ ] Progress bars render properly
- [ ] Mount Wizard modal opens and closes
- [ ] All form fields in Mount Wizard work
- [ ] Power Management modal functions
- [ ] Network Mount Wizard functions
- [ ] All operations (mount, unmount, etc.) still work

## Page 2: FileManager.razor (1,621 lines)

### Overview
Extremely complex file browser with:
- File tree navigation
- Context menus
- File upload/download
- Multiple modals for operations
- Custom styled file list

### Recommended Approach
Given the complexity, consider these strategies:

#### Option A: Incremental Migration
1. Start with the simpler sections (header, toolbar)
2. Migrate one modal at a time
3. Keep file tree and context menus as-is initially
4. Test thoroughly after each section

#### Option B: Component Breakdown
1. Extract file tree into a separate component
2. Extract context menu into a component
3. Migrate each component independently
4. Reassemble with FluentUI styling

#### Option C: Hybrid Approach
1. Migrate the outer shell and modals to FluentUI
2. Keep the file tree and complex interactions with Bootstrap/custom CSS
3. Plan a future refactor for the file tree

### Migration Patterns (Same as DiskManagement)
Follow the same patterns established in other pages:
- Loading states → FluentProgressRing
- Cards → FluentCard
- Tables → fluent-table styling
- Buttons → FluentButton
- Forms → FluentTextField, FluentSelect, etc.
- Modals → FluentDialog
- Alerts → FluentMessageBar
- Badges → FluentBadge

## Common Pitfalls to Avoid

### 1. Unclosed Tags
**Problem**: Forgetting to close FluentUI tags
```razor
<!-- Wrong -->
<FluentCard>
    Content
<!-- Missing </FluentCard> -->

<!-- Correct -->
<FluentCard>
    Content
</FluentCard>
```

### 2. Modal Structure
**Problem**: Mixing Bootstrap and FluentUI modal elements
```razor
<!-- Wrong - Don't mix these -->
<FluentDialog Hidden="@(!showDialog)">
    <div class="modal-header"> <!-- Bootstrap leftover -->
    
<!-- Correct - Use FluentUI throughout -->
<FluentDialog Hidden="@(!showDialog)">
    <FluentDialogHeader>
    <FluentDialogBody>
    <FluentDialogFooter>
</FluentDialog>
```

### 3. Progress Bar Conversion
**Problem**: Not converting percentage to int
```razor
<!-- Wrong -->
<FluentProgress Value="@disk.UsagePercent" Max="100" />

<!-- Correct -->
<FluentProgress Value="@((int)disk.UsagePercent)" Max="100" />
```

### 4. Form Binding
**Problem**: Using wrong binding syntax
```razor
<!-- Wrong -->
<FluentTextField @bind="editingApp.Title" />

<!-- Correct -->
<FluentTextField @bind-Value="editingApp.Title" />
```

## Build and Test Process

### After Each Major Change
1. Build the project: `dotnet build`
2. Check for errors and warnings
3. Fix any compilation issues before proceeding
4. Commit working changes

### Before Final Commit
1. Full build: `dotnet build`
2. Verify 0 errors
3. Run the application if possible
4. Test each migrated page's functionality
5. Update FLUENTUI_MIGRATION_SUMMARY.md

## Resources

### FluentUI Blazor Documentation
- Components: https://www.fluentui-blazor.net/Components
- GitHub: https://github.com/microsoft/fluentui-blazor
- Examples: https://www.fluentui-blazor.net/

### Reference Implementations
Look at these already-migrated pages for patterns:
- **Home.razor** - Progress bars, cards, custom styling
- **Docker.razor** - Tables, badges, action buttons
- **AppManagement.razor** - Complex modal with forms
- **UserManagement.razor** - FluentDialog pattern
- **SystemMonitor.razor** - Table styling, progress bars

## Success Criteria

### DiskManagement.razor Complete When:
- [x] All Bootstrap components removed
- [x] 3 modals converted to FluentDialog
- [x] All tables using fluent-table styling
- [x] All form controls using FluentUI components
- [x] Build succeeds with 0 errors
- [x] All disk operations functional

### FileManager.razor Complete When:
- [x] All Bootstrap components removed
- [x] File browser UI migrated
- [x] Context menus working
- [x] All modals converted to FluentDialog
- [x] Build succeeds with 0 errors
- [x] All file operations functional

## Conclusion

The migration is 84.6% complete with solid patterns established. The remaining pages are complex but follow the same migration patterns demonstrated in the completed pages. Take a methodical approach, test frequently, and don't hesitate to keep working sections stable while migrating others incrementally.

Good luck! 🚀
