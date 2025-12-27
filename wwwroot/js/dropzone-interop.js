// Dropzone.js interop for Blazor
window.dropzoneInterop = {
    instances: {},
    
    init: function(elementId, uploadUrl, currentPath, dotNetHelper) {
        // Clean up any existing instance
        if (this.instances[elementId]) {
            this.instances[elementId].destroy();
        }
        
        const element = document.getElementById(elementId);
        if (!element) {
            console.error('Element not found:', elementId);
            return false;
        }
        
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
                    // Notify Blazor of success
                    if (dotNetHelper) {
                        dotNetHelper.invokeMethodAsync('OnFileUploaded', file.name, true, null);
                    }
                });
                
                this.on("error", function(file, errorMessage) {
                    console.error('Upload error:', errorMessage);
                    // Notify Blazor of error
                    if (dotNetHelper) {
                        const message = typeof errorMessage === 'string' ? errorMessage : errorMessage.message || 'Upload failed';
                        dotNetHelper.invokeMethodAsync('OnFileUploaded', file.name, false, message);
                    }
                });
                
                this.on("complete", function(file) {
                    // Auto-remove file from dropzone after 2 seconds
                    setTimeout(() => {
                        this.removeFile(file);
                    }, 2000);
                });
                
                this.on("queuecomplete", function() {
                    // Notify Blazor that all uploads are complete
                    if (dotNetHelper) {
                        dotNetHelper.invokeMethodAsync('OnAllUploadsComplete');
                    }
                });
            }
        });
        
        this.instances[elementId] = dropzone;
        return true;
    },
    
    destroy: function(elementId) {
        if (this.instances[elementId]) {
            this.instances[elementId].destroy();
            delete this.instances[elementId];
        }
    },
    
    removeAllFiles: function(elementId) {
        if (this.instances[elementId]) {
            this.instances[elementId].removeAllFiles();
        }
    }
};
