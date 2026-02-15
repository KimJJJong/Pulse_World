using Newtonsoft.Json;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Text;

namespace DataConverter;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== Data Converter v7 (Robust) ===");
        
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string root = FindRoot(baseDir) ?? FindRoot(Directory.GetCurrentDirectory());
        
        if (root == null)
        {
            Console.WriteLine("FATAL: Could not find solution root.");
            return;
        }

        string designDir = Path.Combine(root, "Design", "DataTables");
        string serverOut = Path.Combine(root, "Server", "GameServer", "Content", "Data", "Json");
        string clientOut = Path.Combine(root, "Client", "Assets", "Resources", "Data", "Json");

        Console.WriteLine($"[Src] {designDir}");
        
        if (!Directory.Exists(serverOut)) Directory.CreateDirectory(serverOut);
        if (!Directory.Exists(clientOut)) Directory.CreateDirectory(clientOut);

        // Process all CSVs in the folder
        var files = Directory.GetFiles(designDir, "*.csv");
        foreach(var file in files)
        {
             string name = Path.GetFileName(file);
             string jsonName = GetJsonName(name);
             if(jsonName != null)
             {
                 ProcessFile(file, jsonName, serverOut, clientOut);
             }
        }
    }
    
    static string GetJsonName(string csvName)
    {
        if (csvName.Contains("Common_Items")) return "Items.json";
        if (csvName.Contains("Equipments")) return "Equipments.json";
        if (csvName.Contains("Drop_Groups")) return "DropGroups.json";
        return null; // Skip ref tables for now or handle later
    }

    static string FindRoot(string start)
    {
        try
        {
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "Design"))) return dir.FullName;
                dir = dir.Parent;
            }
        }
        catch {}
        return null;
    }

    static void ProcessFile(string path, string jsonName, string out1, string out2)
    {
        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                PrepareHeaderForMatch = args => args.Header.ToLower().Trim(), // Case insensitive, trim spaces
                MissingFieldFound = null,
                HeaderValidated = null,
                HasHeaderRecord = true,
                BadDataFound = null,
            };

            // Use UTF8 with BOM checks
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream, true)) // detect encoding from BOM
            using (var csv = new CsvReader(reader, config))
            {
                var records = new List<Dictionary<string, object>>();
                
                 // Read basic manual way to control types better
                csv.Read();
                csv.ReadHeader();
                string[] headers = csv.HeaderRecord;

                while(csv.Read())
                {
                    var dict = new Dictionary<string, object>();
                    for(int i=0; i<headers.Length; i++)
                    {
                        var rawKey = headers[i];
                        // Strip BOM if present in first key (should be handled by StreamReader but extra safety)
                        if (i == 0 && rawKey.Length > 0 && rawKey[0] == '\uFEFF') rawKey = rawKey.Substring(1);
                        
                        string key = rawKey.Trim();
                        if(string.IsNullOrEmpty(key)) continue;

                        // Get value
                        if(csv.TryGetField(i, out string val))
                        {
                             val = val.Trim();
                             if(int.TryParse(val, out int n)) dict[key] = n;
                             else if(float.TryParse(val, out float f)) dict[key] = f;
                             else if(bool.TryParse(val, out bool b)) dict[key] = b;
                             else dict[key] = val;
                        }
                    }
                    records.Add(dict);
                }

                string json = JsonConvert.SerializeObject(records, Formatting.Indented);
                File.WriteAllText(Path.Combine(out1, jsonName), json);
                File.WriteAllText(Path.Combine(out2, jsonName), json);
                Console.WriteLine($"[OK] {Path.GetFileName(path)} -> {jsonName} ({records.Count})");
            }
        }
        catch(Exception e)
        {
            Console.WriteLine($"[FAIL] {Path.GetFileName(path)}: {e.Message}");
            //Console.WriteLine(e.StackTrace);
        }
    }
}
