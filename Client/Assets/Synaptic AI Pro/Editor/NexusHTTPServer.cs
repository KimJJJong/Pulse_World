#if UNITY_EDITOR
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SynapticPro
{
    /// <summary>
    /// HTTP Server for direct API access to Unity tools
    /// Executes tools on Unity main thread via EditorApplication.update
    /// </summary>
    [InitializeOnLoad]
    public class NexusHTTPServer
    {
        private static NexusHTTPServer _instance;
        public static NexusHTTPServer Instance => _instance ??= new NexusHTTPServer();

        private HttpListener _listener;
        private Thread _listenerThread;
        private bool _isRunning;
        private int _port = 8086;

        // Main thread execution queue
        private static ConcurrentQueue<PendingRequest> _pendingRequests = new ConcurrentQueue<PendingRequest>();
        private static ConcurrentDictionary<string, TaskCompletionSource<string>> _pendingResponses = new ConcurrentDictionary<string, TaskCompletionSource<string>>();
        private static NexusUnityExecutor _executor;

        // Auto-start settings
        private const string PREF_AUTO_START = "SynapticPro_HTTP_AutoStart";
        private const string PREF_HTTP_PORT = "SynapticPro_HTTP_Port";

        public static bool AutoStartEnabled
        {
            get => EditorPrefs.GetBool(PREF_AUTO_START, false);
            set => EditorPrefs.SetBool(PREF_AUTO_START, value);
        }

        public static int SavedPort
        {
            get => EditorPrefs.GetInt(PREF_HTTP_PORT, 8086);
            set => EditorPrefs.SetInt(PREF_HTTP_PORT, value);
        }

        public bool IsRunning => _isRunning;
        public int Port => _port;

        private class PendingRequest
        {
            public string Id;
            public string ToolName;
            public Dictionary<string, object> Parameters;

            public Dictionary<string, string> GetStringParameters()
            {
                var result = new Dictionary<string, string>();
                foreach (var kvp in Parameters)
                {
                    result[kvp.Key] = kvp.Value?.ToString() ?? "";
                }
                return result;
            }
        }

        private static bool _autoStartAttempted = false;

        static NexusHTTPServer()
        {
            EditorApplication.update += ProcessPendingRequests;
            EditorApplication.update += TryAutoStart;
        }

        private static void TryAutoStart()
        {
            if (_autoStartAttempted) return;
            _autoStartAttempted = true;

            // Auto-start if enabled
            if (AutoStartEnabled && !Instance.IsRunning)
            {
                Instance.Start(SavedPort);
                Debug.Log("[Synaptic HTTP] Auto-started on domain reload");
            }
        }

        /// <summary>
        /// Process pending requests on Unity main thread
        /// </summary>
        private static void ProcessPendingRequests()
        {
            int processed = 0;
            while (_pendingRequests.TryDequeue(out var request) && processed < 10)
            {
                processed++;
                try
                {
                    if (_executor == null)
                        _executor = new NexusUnityExecutor();

                    // Convert tool name to operation type
                    var operationType = ConvertToolToOperation(request.ToolName);

                    var operation = new NexusUnityOperation
                    {
                        type = operationType,
                        parameters = request.GetStringParameters()
                    };

                    // Execute on main thread
                    var task = _executor.ExecuteOperation(operation);
                    task.ContinueWith(t =>
                    {
                        if (_pendingResponses.TryRemove(request.Id, out var tcs))
                        {
                            if (t.IsFaulted)
                                tcs.SetException(t.Exception?.InnerException ?? t.Exception);
                            else
                                tcs.SetResult(t.Result);
                        }
                    });
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Synaptic HTTP] Execution error: {e.Message}");
                    if (_pendingResponses.TryRemove(request.Id, out var tcs))
                    {
                        tcs.SetException(e);
                    }
                }
            }
        }

        private static string ConvertToolToOperation(string toolName)
        {
            // Use the same mapping as MCP service
            return NexusEditorMCPService.ConvertMCPToolToOperation(toolName);
        }

        public (bool success, string message) Start(int port = 8086)
        {
            if (_isRunning)
            {
                return (false, "Server already running");
            }

            _port = port;
            SavedPort = port; // Save port for auto-restart

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{port}/");
                _listener.Start();

                _listenerThread = new Thread(ListenForRequests)
                {
                    IsBackground = true
                };
                _listenerThread.Start();

                _isRunning = true;
                var message = $"HTTP server started on port {port}";
                Debug.Log($"[Synaptic HTTP] {message}");
                return (true, message);
            }
            catch (Exception e)
            {
                var message = $"Failed to start HTTP server: {e.Message}";
                Debug.LogError($"[Synaptic HTTP] {message}");
                return (false, message);
            }
        }

        /// <summary>
        /// Enable auto-start and start the server immediately
        /// </summary>
        public (bool success, string message) EnableAutoStart(int port = 8086)
        {
            AutoStartEnabled = true;
            return Start(port);
        }

        /// <summary>
        /// Disable auto-start and optionally stop the server
        /// </summary>
        public (bool success, string message) DisableAutoStart(bool stopServer = false)
        {
            AutoStartEnabled = false;
            if (stopServer && _isRunning)
            {
                return Stop();
            }
            return (true, "Auto-start disabled");
        }

        public (bool success, string message) Stop()
        {
            if (!_isRunning)
            {
                return (false, "Server not running");
            }

            try
            {
                _isRunning = false;
                _listener?.Stop();
                _listener?.Close();
                _listenerThread?.Join(1000);

                var message = "HTTP server stopped";
                Debug.Log($"[Synaptic HTTP] {message}");
                return (true, message);
            }
            catch (Exception e)
            {
                return (false, $"Error stopping server: {e.Message}");
            }
        }

        private void ListenForRequests()
        {
            while (_isRunning)
            {
                try
                {
                    var context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => HandleRequest(context));
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Synaptic HTTP] Listener error: {e.Message}");
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;

            // CORS headers
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            try
            {
                if (request.HttpMethod == "OPTIONS")
                {
                    response.StatusCode = 200;
                    response.Close();
                    return;
                }

                var path = request.Url.AbsolutePath;

                if (request.HttpMethod == "GET")
                {
                    HandleGetRequest(path, response);
                }
                else if (request.HttpMethod == "POST")
                {
                    HandlePostRequest(path, request, response);
                }
                else
                {
                    SendJsonResponse(response, new { error = "Method not allowed" }, 405);
                }
            }
            catch (Exception e)
            {
                SendJsonResponse(response, new { error = e.Message }, 500);
            }
        }

        private void HandleGetRequest(string path, HttpListenerResponse response)
        {
            if (path == "/health")
            {
                SendJsonResponse(response, new
                {
                    status = "ok",
                    server = "Unity HTTP API",
                    port = _port,
                    mainThreadExecution = true
                });
            }
            else if (path == "/tools")
            {
                var toolRegistry = GetToolRegistry();
                SendJsonResponse(response, new
                {
                    message = "Synaptic AI Pro - Unity HTTP API",
                    endpoint = "POST /execute or POST /batch (recommended)",
                    totalTools = toolRegistry.Count,
                    tools = toolRegistry
                });
            }
            else if (path == "/tools/list")
            {
                var tools = GetAvailableTools();
                SendJsonResponse(response, new
                {
                    tools = tools,
                    count = tools.Count
                });
            }
            else if (path == "/categories")
            {
                var categories = GetCategories();
                SendJsonResponse(response, new
                {
                    categories = categories,
                    total_categories = categories.Count
                });
            }
            else if (path.StartsWith("/tools/category/"))
            {
                var category = path.Substring(16); // Remove "/tools/category/"
                var toolsInCat = GetToolsByCategory(category);
                SendJsonResponse(response, new
                {
                    category = category,
                    tools = toolsInCat,
                    count = toolsInCat.Count
                });
            }
            else
            {
                SendJsonResponse(response, new { error = "Not found" }, 404);
            }
        }

        private List<object> GetCategories()
        {
            var categories = new Dictionary<string, int>();
            try
            {
                var registryPath = Path.Combine(Application.dataPath, "Synaptic AI Pro/MCPServer/tool-registry.json");
                if (File.Exists(registryPath))
                {
                    var json = File.ReadAllText(registryPath);
                    var registry = JObject.Parse(json);
                    // tool-registry.json has tools directly at root level (not wrapped in "tools")
                    foreach (var prop in registry.Properties())
                    {
                        var cat = prop.Value?["category"]?.ToString() ?? "Other";
                        if (!categories.ContainsKey(cat))
                            categories[cat] = 0;
                        categories[cat]++;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Synaptic HTTP] Failed to get categories: {e.Message}");
            }

            var result = new List<object>();
            foreach (var kvp in categories.OrderBy(x => x.Key))
            {
                result.Add(new { name = kvp.Key, count = kvp.Value });
            }
            return result;
        }

        private List<object> GetToolsByCategory(string category)
        {
            var tools = new List<object>();
            try
            {
                var registryPath = Path.Combine(Application.dataPath, "Synaptic AI Pro/MCPServer/tool-registry.json");
                if (File.Exists(registryPath))
                {
                    var json = File.ReadAllText(registryPath);
                    var registry = JObject.Parse(json);
                    // tool-registry.json has tools directly at root level
                    foreach (var prop in registry.Properties())
                    {
                        var cat = prop.Value?["category"]?.ToString() ?? "Other";
                        if (cat.Equals(category, StringComparison.OrdinalIgnoreCase))
                        {
                            tools.Add(new
                            {
                                name = prop.Name,
                                title = prop.Value?["title"]?.ToString() ?? prop.Name,
                                description = prop.Value?["description"]?.ToString() ?? "",
                                inputSchema = prop.Value?["inputSchema"]
                            });
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Synaptic HTTP] Failed to get tools by category: {e.Message}");
            }
            return tools;
        }

        private void HandlePostRequest(string path, HttpListenerRequest request, HttpListenerResponse response)
        {
            string body;
            using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
            {
                body = reader.ReadToEnd();
            }

            // POST /execute - Single tool execution (recommended)
            if (path == "/execute")
            {
                try
                {
                    var data = JObject.Parse(body);
                    var toolName = data["tool"]?.ToString();
                    var parameters = data["params"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();

                    if (string.IsNullOrEmpty(toolName))
                    {
                        SendJsonResponse(response, new { error = "Tool name required" }, 400);
                        return;
                    }

                    ExecuteToolAndRespond(toolName, parameters, response);
                }
                catch (Exception e)
                {
                    SendJsonResponse(response, new { error = $"Invalid JSON: {e.Message}" }, 400);
                }
                return;
            }

            // POST /batch - Multiple tool execution (recommended)
            if (path == "/batch")
            {
                try
                {
                    var tasks = JArray.Parse(body);
                    var results = new List<object>();
                    int failed = 0;

                    foreach (var task in tasks)
                    {
                        var toolName = task["tool"]?.ToString();
                        var parameters = task["params"]?.ToObject<Dictionary<string, object>>() ?? new Dictionary<string, object>();

                        if (string.IsNullOrEmpty(toolName))
                        {
                            results.Add(new { tool = (string)null, success = false, error = "Tool name required" });
                            failed++;
                            continue;
                        }

                        var result = ExecuteToolSync(toolName, parameters);
                        var success = result.success;
                        if (!success) failed++;

                        results.Add(new
                        {
                            tool = toolName,
                            success = success,
                            result = result.result,
                            error = result.error
                        });
                    }

                    SendJsonResponse(response, new
                    {
                        success = failed == 0,
                        results = results,
                        executed = results.Count,
                        failed = failed
                    });
                }
                catch (Exception e)
                {
                    SendJsonResponse(response, new { error = $"Invalid JSON array: {e.Message}" }, 400);
                }
                return;
            }

            // POST /tool/:toolName - Legacy direct tool execution
            if (path.StartsWith("/tool/"))
            {
                var toolName = path.Substring(6);

                Dictionary<string, object> parameters;
                try
                {
                    if (string.IsNullOrEmpty(body))
                    {
                        parameters = new Dictionary<string, object>();
                    }
                    else
                    {
                        var jObj = JObject.Parse(body);
                        parameters = jObj.ToObject<Dictionary<string, object>>();
                    }
                }
                catch
                {
                    SendJsonResponse(response, new { error = "Invalid JSON" }, 400);
                    return;
                }

                // Special handling for batch tool - convert operations format to tasks format
                if (toolName == "unity_execute_batch" && parameters.ContainsKey("operations") && !parameters.ContainsKey("tasks"))
                {
                    try
                    {
                        var operationsObj = parameters["operations"];
                        JArray operations;
                        if (operationsObj is JArray ja)
                            operations = ja;
                        else if (operationsObj is string opStr)
                            operations = JArray.Parse(opStr);
                        else
                            operations = JArray.FromObject(operationsObj);

                        var tasks = new JArray();
                        foreach (var op in operations)
                        {
                            var command = op["command"]?.ToString() ?? "";
                            var opType = ConvertToolToOperation(command);

                            var taskParams = new JObject();
                            var opParams = op["parameters"] as JObject;
                            if (opParams != null)
                            {
                                foreach (var prop in opParams.Properties())
                                {
                                    taskParams[prop.Name] = prop.Value?.Type == JTokenType.Object || prop.Value?.Type == JTokenType.Array
                                        ? prop.Value.ToString(Formatting.None)
                                        : prop.Value?.ToString() ?? "";
                                }
                            }

                            tasks.Add(new JObject
                            {
                                ["tool"] = opType,
                                ["parameters"] = taskParams,
                                ["description"] = op["description"]?.ToString() ?? $"{command}"
                            });
                        }

                        parameters["tasks"] = tasks.ToString(Formatting.None);
                        parameters.Remove("operations");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[Synaptic HTTP] Failed to convert batch operations: {e.Message}");
                    }
                }

                // Queue for main thread execution
                var requestId = Guid.NewGuid().ToString();
                var tcs = new TaskCompletionSource<string>();
                _pendingResponses[requestId] = tcs;

                _pendingRequests.Enqueue(new PendingRequest
                {
                    Id = requestId,
                    ToolName = toolName,
                    Parameters = parameters
                });

                try
                {
                    // Wait for result (30s timeout)
                    if (tcs.Task.Wait(TimeSpan.FromSeconds(30)))
                    {
                        var result = tcs.Task.Result;

                        try
                        {
                            var resultObj = JsonConvert.DeserializeObject(result);
                            SendJsonResponse(response, new { success = true, result = resultObj });
                        }
                        catch
                        {
                            SendJsonResponse(response, new { success = true, result });
                        }
                    }
                    else
                    {
                        _pendingResponses.TryRemove(requestId, out _);
                        SendJsonResponse(response, new { error = "Timeout" }, 504);
                    }
                }
                catch (AggregateException ae)
                {
                    var msg = ae.InnerException?.Message ?? ae.Message;
                    SendJsonResponse(response, new { error = msg }, 500);
                }
            }
            else
            {
                SendJsonResponse(response, new { error = "Not found" }, 404);
            }
        }

        private Dictionary<string, object> GetToolRegistry()
        {
            try
            {
                var registryPath = Path.Combine(Application.dataPath, "Synaptic AI Pro/MCPServer/tool-registry.json");
                if (File.Exists(registryPath))
                {
                    var json = File.ReadAllText(registryPath);
                    return JsonConvert.DeserializeObject<Dictionary<string, object>>(json) ?? new Dictionary<string, object>();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Synaptic HTTP] Failed to load tool registry: {e.Message}");
            }
            return new Dictionary<string, object>();
        }

        private List<string> GetAvailableTools()
        {
            var tools = new List<string>();

            // Load from tool-registry.json
            try
            {
                var registryPath = Path.Combine(Application.dataPath, "Synaptic AI Pro/MCPServer/tool-registry.json");
                if (File.Exists(registryPath))
                {
                    var json = File.ReadAllText(registryPath);
                    var registry = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                    if (registry != null)
                    {
                        foreach (var key in registry.Keys)
                        {
                            tools.Add(key);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Synaptic HTTP] Failed to load tool registry: {e.Message}");
            }

            // Fallback if registry failed
            if (tools.Count == 0)
            {
                tools.AddRange(new[]
                {
                    "unity_create_gameobject", "unity_delete_gameobject", "unity_set_transform",
                    "unity_add_component", "unity_get_scene_info", "unity_create_material",
                    "unity_capture_scene_view", "unity_create_skybox_from_image"
                });
            }

            return tools;
        }

        private void ExecuteToolAndRespond(string toolName, Dictionary<string, object> parameters, HttpListenerResponse response)
        {
            var requestId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<string>();
            _pendingResponses[requestId] = tcs;

            _pendingRequests.Enqueue(new PendingRequest
            {
                Id = requestId,
                ToolName = toolName,
                Parameters = parameters
            });

            try
            {
                if (tcs.Task.Wait(TimeSpan.FromSeconds(30)))
                {
                    var result = tcs.Task.Result;
                    try
                    {
                        var resultObj = JsonConvert.DeserializeObject(result);
                        SendJsonResponse(response, new { success = true, result = resultObj });
                    }
                    catch
                    {
                        SendJsonResponse(response, new { success = true, result });
                    }
                }
                else
                {
                    _pendingResponses.TryRemove(requestId, out _);
                    SendJsonResponse(response, new { error = "Timeout" }, 504);
                }
            }
            catch (AggregateException ae)
            {
                var msg = ae.InnerException?.Message ?? ae.Message;
                SendJsonResponse(response, new { error = msg }, 500);
            }
        }

        private (bool success, object result, string error) ExecuteToolSync(string toolName, Dictionary<string, object> parameters)
        {
            var requestId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<string>();
            _pendingResponses[requestId] = tcs;

            _pendingRequests.Enqueue(new PendingRequest
            {
                Id = requestId,
                ToolName = toolName,
                Parameters = parameters
            });

            try
            {
                if (tcs.Task.Wait(TimeSpan.FromSeconds(30)))
                {
                    var result = tcs.Task.Result;
                    try
                    {
                        var resultObj = JsonConvert.DeserializeObject(result);
                        return (true, resultObj, null);
                    }
                    catch
                    {
                        return (true, result, null);
                    }
                }
                else
                {
                    _pendingResponses.TryRemove(requestId, out _);
                    return (false, null, "Timeout");
                }
            }
            catch (AggregateException ae)
            {
                return (false, null, ae.InnerException?.Message ?? ae.Message);
            }
        }

        private void SendJsonResponse(HttpListenerResponse response, object data, int statusCode = 200)
        {
            response.StatusCode = statusCode;
            response.ContentType = "application/json";

            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            var buffer = Encoding.UTF8.GetBytes(json);

            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.OutputStream.Close();
        }
    }
}
#endif
