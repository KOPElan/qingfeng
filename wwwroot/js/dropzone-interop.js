// Dropzone.js interop for Blazor
window.dropzoneInterop = {
    instances: {},
    pendingTimeouts: {},
    
    // Configuration constants
    AUTO_REMOVE_DELAY_MS: 2000,
    
    init: function(elementId, uploadUrl, currentPath, dotNetHelper) {
        // Clean up any existing instance
        if (this.instances[elementId]) {
            this.destroy(elementId);
        }
        
        const element = document.getElementById(elementId);
        if (!element) {
            console.error('Dropzone initialization failed: element not found:', elementId);
            throw new Error('Dropzone initialization failed: element not found: ' + elementId);
        }
        
        // Store timeouts for this instance
        this.pendingTimeouts[elementId] = [];
        
        // Initialize Dropzone
        const dropzone = new Dropzone(element, {
            url: uploadUrl,
            paramName: "file",
            maxFilesize: 500, // MB
            parallelUploads: 5,
            uploadMultiple: false,
            autoProcessQueue: true,
            addRemoveLinks: true,
            dictDefaultMessage: "拖拽文件到此处或点击上传",
            dictFallbackMessage: "您的浏览器不支持拖拽文件上传",
            dictFileTooBig: "文件太大 ({{filesize}}MB). 最大文件大小: {{maxFilesize}}MB.",
            dictInvalidFileType: "不支持的文件类型",
            dictResponseError: "服务器响应错误 {{statusCode}}",
            dictCancelUpload: "取消上传",
            dictCancelUploadConfirmation: "确定要取消上传吗？",
            dictRemoveFile: "删除文件",
            dictMaxFilesExceeded: "您不能上传更多文件",
            
            init: function() {
                this.on("sending", function(file, xhr, formData) {
                    // Add directory path to the request
                    formData.append("directoryPath", currentPath);
                });
                
                this.on("success", function(file, response) {
                    console.log('File uploaded successfully:', file.name);
                    // Notify Blazor of success with null check
                    if (dotNetHelper) {
                        try {
                            dotNetHelper.invokeMethodAsync('OnFileUploaded', file.name, true, null);
                        } catch (e) {
                            console.warn('Failed to invoke OnFileUploaded:', e);
                        }
                    }
                });
                
                this.on("error", function(file, errorMessage) {
                    console.error('Upload error:', errorMessage);
                    // Notify Blazor of error with null check
                    if (dotNetHelper) {
                        try {
                            const message = typeof errorMessage === 'string' ? errorMessage : errorMessage.message || 'Upload failed';
                            dotNetHelper.invokeMethodAsync('OnFileUploaded', file.name, false, message);
                        } catch (e) {
                            console.warn('Failed to invoke OnFileUploaded:', e);
                        }
                    }
                });
                
                this.on("complete", function(file) {
                    // Auto-remove file from dropzone after delay
                    const timeoutId = setTimeout(() => {
                        // Check if the instance still exists before removing
                        if (window.dropzoneInterop &&
                            window.dropzoneInterop.instances &&
                            window.dropzoneInterop.instances[elementId] === this) {
                            try {
                                this.removeFile(file);
                            } catch (e) {
                                console.warn('Failed to remove file:', e);
                            }
                        }
                    }, window.dropzoneInterop.AUTO_REMOVE_DELAY_MS);
                    
                    // Track the timeout so we can clear it on destroy
                    if (window.dropzoneInterop.pendingTimeouts[elementId]) {
                        window.dropzoneInterop.pendingTimeouts[elementId].push(timeoutId);
                    }
                });
                
                this.on("queuecomplete", function() {
                    // Notify Blazor that all uploads are complete with null check
                    if (dotNetHelper) {
                        try {
                            dotNetHelper.invokeMethodAsync('OnAllUploadsComplete');
                        } catch (e) {
                            console.warn('Failed to invoke OnAllUploadsComplete:', e);
                        }
                    }
                });
            }
        });
        
        this.instances[elementId] = dropzone;
        return true;
    },
    
    destroy: function(elementId) {
        // Clear all pending timeouts for this instance
        if (this.pendingTimeouts[elementId]) {
            this.pendingTimeouts[elementId].forEach(timeoutId => clearTimeout(timeoutId));
            delete this.pendingTimeouts[elementId];
        }
        
        // Destroy the Dropzone instance
        if (this.instances[elementId]) {
            try {
                this.instances[elementId].destroy();
            } catch (e) {
                console.warn('Error destroying Dropzone instance:', e);
            }
            delete this.instances[elementId];
        }
    },
    
    removeAllFiles: function(elementId) {
        if (this.instances[elementId]) {
            try {
                this.instances[elementId].removeAllFiles();
            } catch (e) {
                console.warn('Error removing files:', e);
            }
        }
    }
};
