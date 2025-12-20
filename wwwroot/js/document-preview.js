// Document Preview Helper Functions
// Handles PDF and Office document rendering

window.documentPreview = {
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
            return { success: false, error: error.message };
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
            
            container.innerHTML = `<div class="docx-content" style="padding: 20px; background: white; color: black; max-width: 800px; margin: 0 auto;">${result.value}</div>`;
            
            return { success: true, warnings: result.messages };
        } catch (error) {
            console.error('Error loading DOCX:', error);
            return { success: false, error: error.message };
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
                
                tabButton.onclick = function() {
                    // Update tab styles
                    document.querySelectorAll('.excel-tab-button').forEach(btn => {
                        btn.style.background = '#f3f3f3';
                        btn.style.color = 'black';
                    });
                    this.style.background = '#0078d4';
                    this.style.color = 'white';
                    
                    // Show corresponding sheet
                    const sheet = workbook.Sheets[sheetName];
                    const html = XLSX.utils.sheet_to_html(sheet);
                    contentDiv.innerHTML = `<div style="padding: 10px;">${html}</div>`;
                    
                    // Style the table
                    const table = contentDiv.querySelector('table');
                    styleTable(table);
                };
                
                tabsDiv.appendChild(tabButton);
            });
            
            container.appendChild(tabsDiv);
            container.appendChild(contentDiv);
            
            // Show first sheet by default
            if (workbook.SheetNames.length > 0) {
                const firstSheet = workbook.Sheets[workbook.SheetNames[0]];
                const html = XLSX.utils.sheet_to_html(firstSheet);
                contentDiv.innerHTML = `<div style="padding: 10px;">${html}</div>`;
                
                // Style the table
                const table = contentDiv.querySelector('table');
                styleTable(table);
            }
            
            return { success: true, sheetCount: workbook.SheetNames.length };
        } catch (error) {
            console.error('Error loading Excel:', error);
            return { success: false, error: error.message };
        }
    },

    // Cleanup function
    cleanup: function (containerId) {
        const container = document.getElementById(containerId);
        if (container) {
            container.innerHTML = '';
        }
    }
};
