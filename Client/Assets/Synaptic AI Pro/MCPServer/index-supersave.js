/**
 * Synaptic AI Pro - Token SuperSave Mode
 *
 * Experimental: Only 3 meta-tools for 99% context reduction.
 * Compatible with all MCP clients without dynamic tool loading support.
 *
 * Tools:
 * 1. list_categories() - Show available tool categories
 * 2. list_tools(category) - Show tools in a category with their schemas
 * 3. execute(tool_name, params) - Run any tool by name
 */

const express = require('express');
const http = require('http');
const WebSocket = require('ws');
const cors = require('cors');
const { z } = require('zod');
const { createServer } = require('./mcp-server');
const fs = require('fs');
const path = require('path');
const os = require('os');

const app = express();
app.use(cors());
app.use(express.json());

const server = http.createServer(app);
let wss = null;

let unityWebSocket = null;
let mcpServer = null;

// =====================================================
// Load Tool Registry from JSON file
// =====================================================
let TOOL_REGISTRY_RAW = {};
let CATEGORIES = {};
let ALL_TOOLS = {};

function loadToolRegistry() {
    try {
        const registryPath = path.join(__dirname, 'tool-registry.json');
        const data = fs.readFileSync(registryPath, 'utf8');
        TOOL_REGISTRY_RAW = JSON.parse(data);

        // Build categories from registry
        CATEGORIES = {};
        ALL_TOOLS = {};

        for (const [toolName, toolData] of Object.entries(TOOL_REGISTRY_RAW)) {
            const category = (toolData.category || 'Other').toLowerCase();

            if (!CATEGORIES[category]) {
                CATEGORIES[category] = {
                    description: `${toolData.category} tools`,
                    tools: {}
                };
            }

            // Store tool info (use clean name without unity_ prefix as primary key)
            const cleanName = toolName.replace(/^unity_/, '');
            CATEGORIES[category].tools[cleanName] = {
                fullName: toolName,
                title: toolData.title,
                description: toolData.description
            };

            // Only store clean name to avoid duplication
            ALL_TOOLS[cleanName] = {
                fullName: toolName,
                category: category,
                title: toolData.title,
                description: toolData.description
            };
        }

        console.error(`[SuperSave] Loaded ${Object.keys(TOOL_REGISTRY_RAW).length} tools from tool-registry.json`);
    } catch (err) {
        console.error('[SuperSave] Failed to load tool-registry.json:', err.message);
    }
}

// Load on startup
loadToolRegistry();

// =====================================================
// WebSocket Setup (same as index.js)
// =====================================================
function setupWebSocketHandlers() {
    if (!wss) return;

    wss.on('connection', (ws, req) => {
        const isUnity = req.headers['x-client-type'] === 'unity' || req.url === '/unity';

        if (isUnity || !req.url.includes('mcp')) {
            if (unityWebSocket) {
                unityWebSocket.close();
            }
            unityWebSocket = ws;

            ws.on('message', async (message) => {
                try {
                    const data = JSON.parse(message);
                    const responseId = data.id || data.operationId;
                    if ((data.type === 'operation_result' || data.type === 'operation_response') && responseId) {
                        const numericId = typeof responseId === 'string' ? parseInt(responseId) : responseId;
                        if (pendingRequests.has(numericId)) {
                            const { resolve, reject, timeout } = pendingRequests.get(numericId);
                            clearTimeout(timeout);
                            pendingRequests.delete(numericId);
                            if (data.success !== false) {
                                resolve(data.content || data.result);
                            } else {
                                reject(new Error(data.content || data.error || 'Unity command failed'));
                            }
                        }
                    }
                } catch (e) {}
            });

            ws.on('close', () => {
                unityWebSocket = null;
            });
        }
    });
}

// Unity command helper
const pendingRequests = new Map();
let requestId = 0;
const sleep = (ms) => new Promise(resolve => setTimeout(resolve, ms));

async function sendUnityCommandOnce(command, params, id) {
    return new Promise((resolve, reject) => {
        const timeout = setTimeout(() => {
            pendingRequests.delete(id);
            reject(new Error('timeout'));
        }, 15000);

        pendingRequests.set(id, { resolve, reject, timeout });

        const message = JSON.stringify({
            type: 'unity_operation',
            command: command,
            parameters: {
                ...params,
                operationId: id.toString()
            }
        });
        unityWebSocket.send(message);
    });
}

async function sendUnityCommand(command, params = {}) {
    const MAX_RETRIES = 30;
    const RETRY_DELAY = 10000;

    for (let attempt = 1; attempt <= MAX_RETRIES; attempt++) {
        if (!unityWebSocket || unityWebSocket.readyState !== WebSocket.OPEN) {
            if (attempt < MAX_RETRIES) {
                console.error(`[SuperSave] Unity not connected (attempt ${attempt}/${MAX_RETRIES}). Waiting...`);
                await sleep(RETRY_DELAY);
                continue;
            }
            throw new Error('Unity not connected');
        }

        const id = ++requestId;
        try {
            const result = await sendUnityCommandOnce(command, params, id);
            return result;
        } catch (error) {
            if (attempt < MAX_RETRIES) {
                console.error(`[SuperSave] Command failed (attempt ${attempt}): ${error.message}`);
                await sleep(RETRY_DELAY);
            }
        }
    }
    throw new Error('Unity not connected after retries');
}

// =====================================================
// MCP Server with 3 Meta-Tools
// =====================================================
async function setupMCPServer() {
    mcpServer = createServer();

    // ===== META-TOOL 1: list_categories =====
    mcpServer.registerTool('list_categories', {
        title: 'List Tool Categories',
        description: 'List all available tool categories. Use this first to discover what tools are available.',
        inputSchema: z.object({})
    }, async () => {
        const categories = Object.entries(CATEGORIES).map(([name, data]) => ({
            name,
            description: data.description,
            toolCount: Object.keys(data.tools).length
        }));

        const totalTools = Object.keys(TOOL_REGISTRY_RAW).length;

        return {
            content: [{
                type: 'text',
                text: `Available Categories (${categories.length} categories, ${totalTools} total tools):\n\n` +
                    categories.map(c => `• ${c.name} (${c.toolCount} tools)\n  ${c.description}`).join('\n\n') +
                    '\n\nUse list_tools(category) to see tools in a specific category.'
            }]
        };
    });

    // ===== META-TOOL 2: list_tools =====
    mcpServer.registerTool('list_tools', {
        title: 'List Tools in Category',
        description: 'List all tools in a specific category with their parameters. Use this to learn how to use specific tools.',
        inputSchema: z.object({
            category: z.string().describe('Category name (e.g., "gameobject", "material", "lighting")')
        })
    }, async (params) => {
        const category = params.category.toLowerCase();

        if (!CATEGORIES[category]) {
            const availableCategories = Object.keys(CATEGORIES).join(', ');
            return {
                content: [{
                    type: 'text',
                    text: `Unknown category: "${category}"\n\nAvailable categories: ${availableCategories}`
                }]
            };
        }

        const categoryData = CATEGORIES[category];
        const tools = Object.entries(categoryData.tools).map(([name, data]) => {
            return `• ${name} (${data.fullName})\n  ${data.title}\n  ${data.description}`;
        });

        return {
            content: [{
                type: 'text',
                text: `Category: ${category}\n${categoryData.description}\n\nTools (${tools.length}):\n\n${tools.join('\n\n')}\n\nUse execute(tool_name, params) to run a tool.`
            }]
        };
    });

    // ===== META-TOOL 3: execute =====
    mcpServer.registerTool('execute', {
        title: 'Execute Tool',
        description: 'Execute any Unity tool by name. Use list_tools(category) first to see available tools and their parameters.',
        inputSchema: z.object({
            tool: z.string().describe('Tool name to execute (e.g., "create_gameobject", "set_transform")'),
            params: z.any().optional().describe('Parameters as JSON object {"name":"value"}')
        })
    }, async (params) => {
        const toolName = params.tool;
        let toolParams = params.params || {};

        // Handle case where params is passed as string (e.g., '{"name":"x"}')
        if (typeof toolParams === 'string') {
            try {
                toolParams = JSON.parse(toolParams);
            } catch (e) {
                // Try to parse key=value format (e.g., 'name=MyCube')
                const keyValueMatch = toolParams.match(/^(\w+)=(.+)$/);
                if (keyValueMatch) {
                    toolParams = { [keyValueMatch[1]]: keyValueMatch[2] };
                } else {
                    // Treat as single value if it's just a plain string
                    toolParams = {};
                }
            }
        }

        // Normalize tool name - strip unity_ prefix if present
        const strippedName = toolName.startsWith('unity_') ? toolName.substring(6) : toolName;
        const fullName = `unity_${strippedName}`;

        // Check if tool exists in registry
        const toolInfo = ALL_TOOLS[strippedName];
        if (!toolInfo) {
            // Find similar tool names for helpful error message
            const allToolNames = Object.keys(ALL_TOOLS);
            const similar = allToolNames.filter(t =>
                t.includes(strippedName) || strippedName.includes(t) ||
                t.split('_').some(part => strippedName.includes(part))
            ).slice(0, 5);

            let errorMsg = `Unknown tool: "${toolName}"`;
            if (similar.length > 0) {
                errorMsg += `\n\nDid you mean: ${similar.join(', ')}?`;
            }
            errorMsg += `\n\nUse list_categories() to see available categories, then list_tools(category) to see tools.`;

            return {
                content: [{
                    type: 'text',
                    text: errorMsg
                }]
            };
        }

        // Get the command name (without unity_ prefix) for Unity
        // Use lowercase - Unity's ConvertCommandToOperationType expects lowercase
        const commandName = strippedName.toLowerCase();

        try {
            const result = await sendUnityCommand(commandName, toolParams);
            return {
                content: [{
                    type: 'text',
                    text: typeof result === 'string' ? result : JSON.stringify(result, null, 2)
                }]
            };
        } catch (error) {
            // Detailed error message
            let errorDetail = `Error executing "${strippedName}":\n`;
            errorDetail += `  Message: ${error.message}\n`;

            if (error.message.includes('not connected')) {
                errorDetail += `\nTroubleshooting:\n`;
                errorDetail += `  1. Check Unity Editor is running\n`;
                errorDetail += `  2. Verify Synaptic AI Pro is connected (check Console)\n`;
                errorDetail += `  3. Try restarting the MCP server\n`;
            } else if (error.message.includes('timeout')) {
                errorDetail += `\nThe command timed out. Unity may be:\n`;
                errorDetail += `  - Compiling scripts\n`;
                errorDetail += `  - Processing a heavy operation\n`;
                errorDetail += `  - Not responding\n`;
            }

            errorDetail += `\nTool info: ${toolInfo.title} (${toolInfo.category})`;

            return {
                content: [{
                    type: 'text',
                    text: errorDetail
                }]
            };
        }
    });

    // Start MCP server
    await mcpServer.start();
}

// =====================================================
// Main Entry Point
// =====================================================
async function main() {
    const PORT = process.env.PORT || 8090;

    // Start WebSocket server
    wss = new WebSocket.Server({ server });
    setupWebSocketHandlers();

    // Setup and start MCP
    await setupMCPServer();

    server.listen(PORT, () => {
        console.error(`[SuperSave] Token SuperSave Mode started on port ${PORT}`);
        console.error(`[SuperSave] Only 3 meta-tools loaded (99% context reduction)`);
    });
}

// Shutdown handler
function shutdownServer() {
    if (unityWebSocket && unityWebSocket.readyState === WebSocket.OPEN) {
        unityWebSocket.close();
    }
    if (wss) {
        wss.close();
    }
    if (server && server.listening) {
        server.close(() => {
            process.exit(0);
        });
    } else {
        process.exit(0);
    }
    setTimeout(() => {
        process.exit(1);
    }, 5000);
}

process.on('SIGINT', shutdownServer);
process.on('SIGTERM', shutdownServer);
process.stdin.on('close', () => {
    shutdownServer();
});

process.on('uncaughtException', (error) => {
    // Silent
});

process.on('unhandledRejection', (reason, promise) => {
    // Silent
});

main().catch(err => {
    console.error('[SuperSave] Fatal error:', err);
    process.exit(1);
});
