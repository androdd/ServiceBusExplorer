using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace ServiceBusExplorer.Common.Helpers.Modeshift
{
    public class TenantsProvider
    {
        private Dictionary<string, string> tenants;

        public void Init(string tenantConfigPath)
        {
            tenants = GetTenantIdsFromFiles(tenantConfigPath);
        }

        private static Dictionary<string, string> GetTenantIdsFromFiles(string directoryPath)
        {
            var tenantIdDictionary = new Dictionary<string, string>();

            if (Directory.Exists(directoryPath))
            {
                var xmlFiles = Directory.GetFiles(directoryPath, "*.xml");

                foreach (var file in xmlFiles)
                {
                    string tenantId = GetTenantIdFromFile(file);

                    if (!string.IsNullOrEmpty(tenantId))
                    {
                        tenantIdDictionary[tenantId] = Path.GetFileNameWithoutExtension(file);
                    }
                }
            }
            else
            {
                throw new DirectoryNotFoundException("The specified directory was not found.");
            }

            return tenantIdDictionary;
        }

        private static string GetTenantIdFromFile(string filePath)
        {
            try
            {
                XDocument doc = XDocument.Load(filePath);
                XNamespace ns = "http://schemas.microsoft.com/2011/01/fabric";

                var tenantIdElement = doc.Root
                                        .Element(ns + "Parameters")
                                        .Elements(ns + "Parameter")
                                        .FirstOrDefault(p => p.Attribute("Name")?.Value == "Tenant_TenantId");

                return tenantIdElement?.Attribute("Value")?.Value;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing file {filePath}: {ex.Message}");
                return null;
            }
        }

        public string SetName(string name)
        {
            var parts = name.Split('.');

            if (parts.Length != 2 || !tenants.ContainsKey(parts[1]))
            {
                return name;
            }

            name = parts[0] + "." + tenants[parts[1]];

            return name;
        }
    }
}
