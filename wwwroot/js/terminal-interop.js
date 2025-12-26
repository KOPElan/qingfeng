// Terminal interop for xterm.js
window.terminalInterop = (function () {
    let terminal = null;
    let fitAddon = null;
    let dotNetRef = null;
    let resizeDotNetRef = null;
    let resizeHandler = null;
    let dataDisposable = null;

    return {
        initialize: function () {
            if (typeof Terminal === 'undefined') {
                console.error('xterm.js not loaded');
                return;
            }

            // Create terminal instance
            terminal = new Terminal({
                cursorBlink: true,
                fontSize: 14,
                fontFamily: 'Menlo, Monaco, "Courier New", monospace',
                theme: {
                    background: '#000000',
                    foreground: '#ffffff',
                    cursor: '#ffffff',
                    cursorAccent: '#000000',
                    selection: 'rgba(255, 255, 255, 0.3)',
                    black: '#000000',
                    red: '#e06c75',
                    green: '#98c379',
                    yellow: '#d19a66',
                    blue: '#61afef',
                    magenta: '#c678dd',
                    cyan: '#56b6c2',
                    white: '#abb2bf',
                    brightBlack: '#5c6370',
                    brightRed: '#e06c75',
                    brightGreen: '#98c379',
                    brightYellow: '#d19a66',
                    brightBlue: '#61afef',
                    brightMagenta: '#c678dd',
                    brightCyan: '#56b6c2',
                    brightWhite: '#ffffff'
                },
                convertEol: true,
                scrollback: 1000,
                tabStopWidth: 4
            });

            // Load fit addon if available
            if (typeof FitAddon !== 'undefined') {
                fitAddon = new FitAddon.FitAddon();
                terminal.loadAddon(fitAddon);
            }

            // Open terminal in the DOM
            const terminalElement = document.getElementById('terminal');
            if (terminalElement) {
                terminal.open(terminalElement);
                
                // Fit terminal to container
                if (fitAddon) {
                    setTimeout(() => {
                        fitAddon.fit();
                    }, 100);
                }

                // Handle window resize - store reference for cleanup
                resizeHandler = () => {
                    if (fitAddon && terminal) {
                        fitAddon.fit();
                        if (resizeDotNetRef) {
                            resizeDotNetRef.invokeMethodAsync('ResizeTerminal', 
                                terminal.rows, terminal.cols);
                        }
                    }
                };
                window.addEventListener('resize', resizeHandler);
            }
        },

        writeOutput: function (data) {
            if (terminal) {
                terminal.write(data);
            }
        },

        onData: function (dotNetReference) {
            dotNetRef = dotNetReference;
            if (terminal) {
                // Dispose previous handler if exists
                if (dataDisposable) {
                    dataDisposable.dispose();
                }
                // Store disposable for cleanup
                dataDisposable = terminal.onData(data => {
                    if (dotNetRef) {
                        dotNetRef.invokeMethodAsync('SendInput', data);
                    }
                });
            }
        },

        onResize: function (dotNetReference) {
            resizeDotNetRef = dotNetReference;
            if (terminal && fitAddon) {
                // Send initial size
                setTimeout(() => {
                    if (resizeDotNetRef) {
                        resizeDotNetRef.invokeMethodAsync('ResizeTerminal', 
                            terminal.rows, terminal.cols);
                    }
                }, 100);
            }
        },

        clear: function () {
            if (terminal) {
                terminal.clear();
            }
        },

        focus: function () {
            if (terminal) {
                terminal.focus();
            }
        },

        dispose: function () {
            // Remove window resize listener
            if (resizeHandler) {
                window.removeEventListener('resize', resizeHandler);
                resizeHandler = null;
            }
            
            // Dispose data event handler
            if (dataDisposable) {
                dataDisposable.dispose();
                dataDisposable = null;
            }
            
            // Dispose terminal
            if (terminal) {
                terminal.dispose();
                terminal = null;
            }
            
            fitAddon = null;
            dotNetRef = null;
            resizeDotNetRef = null;
        }
    };
})();
