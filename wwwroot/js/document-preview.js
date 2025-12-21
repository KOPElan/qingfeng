// Document Preview Helper Functions
// Handles PDF and Office document rendering

window.documentPreview = {
    // Simple HTML sanitizer (basic XSS protection)
    sanitizeHtml: function(html) {
        // If DOMPurify is available, use it (recommended)
        if (window.DOMPurify) {
            return window.DOMPurify.sanitize(html);
        }
        
        // Fallback: If DOMPurify is not loaded, return empty string for safety
        // This ensures we don't attempt inadequate sanitization
        console.warn('DOMPurify not loaded. Cannot safely sanitize HTML content.');
        return '<div style="padding:16px;color:#c00;">无法安全地显示此文档内容。请确保 DOMPurify 库已加载。</div>';
    },
    
    // Display error message in container
    displayError: function(containerId, message) {
        const container = document.getElementById(containerId);
        if (container) {
            // Safely set error message using textContent to prevent injection
            const errorDiv = document.createElement('div');
            errorDiv.style.padding = '16px';
            errorDiv.style.color = 'var(--mud-palette-error)';
            errorDiv.style.textAlign = 'center';
            errorDiv.textContent = message;
            
            // Add download suggestion
            const downloadHint = document.createElement('br');
            const hintText = document.createElement('span');
            hintText.textContent = '请下载文件后使用本地应用程序打开。';
            
            errorDiv.appendChild(downloadHint);
            errorDiv.appendChild(hintText);
            
            container.innerHTML = '';
            container.appendChild(errorDiv);
        }
    },
    
    // Initialize PDF.js viewer
    initPdfViewer: async function (containerId, fileUrl) {
        try {
            // Load PDF.js from CDN
            if (typeof pdfjsLib === 'undefined') {
                console.error('PDF.js library not loaded');
                return { success: false, error: 'PDF.js library not loaded' };
            }

            const loadingTask = pdfjsLib.getDocument(fileUrl);
            const pdf = await loadingTask.promise;
            
            const container = document.getElementById(containerId);
            if (!container) {
                return { success: false, error: 'Container not found' };
            }
            
            container.innerHTML = ''; // Clear previous content
            
            // Render all pages
            for (let pageNum = 1; pageNum <= pdf.numPages; pageNum++) {
                const page = await pdf.getPage(pageNum);
                const scale = 1.5;
                const viewport = page.getViewport({ scale: scale });

                // Create canvas for each page
                const canvas = document.createElement('canvas');
                canvas.className = 'pdf-page-canvas';
                canvas.style.display = 'block';
                canvas.style.margin = '10px auto';
                canvas.style.border = '1px solid #ddd';
                
                const context = canvas.getContext('2d');
                canvas.height = viewport.height;
                canvas.width = viewport.width;

                const renderContext = {
                    canvasContext: context,
                    viewport: viewport
                };

                await page.render(renderContext).promise;
                container.appendChild(canvas);
            }

            return { success: true, pageCount: pdf.numPages };
        } catch (error) {
            console.error('Error loading PDF:', error);
            let errorMessage = 'PDF加载失败';
            if (error.message) {
                if (error.message.includes('CORS')) {
                    errorMessage = 'PDF加载失败: 跨域请求被阻止';
                } else if (error.message.includes('Invalid PDF')) {
                    errorMessage = 'PDF加载失败: 文件已损坏或格式无效';
                } else {
                    errorMessage = `PDF加载失败: ${error.message}`;
                }
            }
            return { success: false, error: errorMessage };
        }
    },

    // Initialize Office document viewer using Mammoth.js for DOCX
    initDocxViewer: async function (containerId, fileUrl) {
        try {
            if (typeof mammoth === 'undefined') {
                console.error('Mammoth.js library not loaded');
                return { success: false, error: 'Mammoth.js library not loaded' };
            }

            const response = await fetch(fileUrl);
            const arrayBuffer = await response.arrayBuffer();
            
            const result = await mammoth.convertToHtml({ arrayBuffer: arrayBuffer });
            
            const container = document.getElementById(containerId);
            if (!container) {
                return { success: false, error: 'Container not found' };
            }
            
            // Sanitize HTML before inserting to prevent XSS
            const sanitizedHtml = this.sanitizeHtml(result.value);
            
            container.innerHTML = `<div class="docx-content" style="padding: 20px; max-width: 800px; margin: 0 auto;">${sanitizedHtml}</div>`;
            
            return { success: true, warnings: result.messages };
        } catch (error) {
            console.error('Error loading DOCX:', error);
            let errorMessage = 'Word文档加载失败';
            if (error.message) {
                if (error.message.includes('fetch')) {
                    errorMessage = 'Word文档加载失败: 无法获取文件';
                } else {
                    errorMessage = `Word文档加载失败: ${error.message}`;
                }
            }
            return { success: false, error: errorMessage };
        }
    },

    // Initialize Excel viewer using SheetJS (xlsx.js)
    initXlsxViewer: async function (containerId, fileUrl) {
        try {
            if (typeof XLSX === 'undefined') {
                console.error('SheetJS library not loaded');
                return { success: false, error: 'SheetJS library not loaded' };
            }

            const response = await fetch(fileUrl);
            const arrayBuffer = await response.arrayBuffer();
            
            const workbook = XLSX.read(arrayBuffer, { type: 'array' });
            
            const container = document.getElementById(containerId);
            if (!container) {
                return { success: false, error: 'Container not found' };
            }
            
            container.innerHTML = '';
            
            // Create tabs for each sheet
            const tabsDiv = document.createElement('div');
            tabsDiv.className = 'excel-tabs';
            tabsDiv.style.marginBottom = '10px';
            tabsDiv.style.borderBottom = '1px solid #ddd';
            
            const contentDiv = document.createElement('div');
            contentDiv.className = 'excel-content';
            contentDiv.style.overflow = 'auto';
            contentDiv.style.maxHeight = '600px';
            
            // Helper function to style table
            const styleTable = function(table) {
                if (table) {
                    table.style.borderCollapse = 'collapse';
                    table.style.width = '100%';
                    table.querySelectorAll('td, th').forEach(cell => {
                        cell.style.border = '1px solid #ddd';
                        cell.style.padding = '8px';
                    });
                    table.querySelectorAll('th').forEach(header => {
                        header.style.background = '#f3f3f3';
                        header.style.fontWeight = 'bold';
                    });
                }
            };
            
            // Helper function to sanitize and render sheet
            const renderSheet = function(sheetName) {
                const sheet = workbook.Sheets[sheetName];
                const html = XLSX.utils.sheet_to_html(sheet);
                // Sanitize HTML from SheetJS to prevent XSS
                const sanitizedHtml = window.documentPreview.sanitizeHtml(html);
                contentDiv.innerHTML = `<div style="padding: 10px;">${sanitizedHtml}</div>`;
                
                // Style the table
                const table = contentDiv.querySelector('table');
                styleTable(table);
            };
            
            let activeTabButton = null;
            
            workbook.SheetNames.forEach((sheetName, index) => {
                // Create tab button
                const tabButton = document.createElement('button');
                tabButton.textContent = sheetName;
                tabButton.className = 'excel-tab-button';
                tabButton.style.padding = '10px 20px';
                tabButton.style.border = 'none';
                tabButton.style.background = index === 0 ? '#0078d4' : '#f3f3f3';
                tabButton.style.color = index === 0 ? 'white' : 'black';
                tabButton.style.cursor = 'pointer';
                tabButton.style.marginRight = '5px';
                
                if (index === 0) {
                    activeTabButton = tabButton;
                }
                
                tabButton.onclick = function() {
                    // Update tab styles - use stored reference instead of querying all buttons
                    if (activeTabButton) {
                        activeTabButton.style.background = '#f3f3f3';
                        activeTabButton.style.color = 'black';
                    }
                    this.style.background = '#0078d4';
                    this.style.color = 'white';
                    activeTabButton = this;
                    
                    // Show corresponding sheet
                    renderSheet(sheetName);
                };
                
                tabsDiv.appendChild(tabButton);
            });
            
            container.appendChild(tabsDiv);
            container.appendChild(contentDiv);
            
            // Show first sheet by default
            if (workbook.SheetNames.length > 0) {
                renderSheet(workbook.SheetNames[0]);
            }
            
            return { success: true, sheetCount: workbook.SheetNames.length };
        } catch (error) {
            console.error('Error loading Excel:', error);
            let errorMessage = 'Excel文件加载失败';
            if (error.message) {
                if (error.message.includes('fetch')) {
                    errorMessage = 'Excel文件加载失败: 无法获取文件';
                } else {
                    errorMessage = `Excel文件加载失败: ${error.message}`;
                }
            }
            return { success: false, error: errorMessage };
        }
    },

    // Cleanup function
    cleanup: function (containerId) {
        const container = document.getElementById(containerId);
        if (container) {
            // Replace the container with a clone to remove all event listeners
            // This prevents memory leaks from accumulated event handlers
            if (container.parentNode) {
                const newContainer = container.cloneNode(false); // shallow clone, keeps attributes (including id)
                container.parentNode.replaceChild(newContainer, container);
            } else {
                // Fallback: if the container has no parent, just clear its contents
                container.innerHTML = '';
            }
        }
    }
};
