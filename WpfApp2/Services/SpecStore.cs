using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WpfApp2.Model;
using System.Text.Json;
using System.IO;

namespace WpfApp2.Services
{
    public class SpecStore
    {
        public static VoltageSpec Spec { get; set; }
        public static string path;

        public static VoltageSpec Load()
        {
            path = Path.Combine(AppContext.BaseDirectory, "spec.json");
            if (!File.Exists(path))
            {
                Spec = new VoltageSpec();
                return Spec;
            }

            string json = File.ReadAllText(path);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            Spec = JsonSerializer.Deserialize<VoltageSpec>(json, options) ?? new VoltageSpec();
            return Spec;
        }
    }
}
