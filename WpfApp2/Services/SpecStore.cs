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

        /// <summary>
        /// 阈值写入成功后通知。MainViewModel 和窗口同寿，订这个静态事件不用退订。
        /// </summary>
        public static event EventHandler Changed;

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

        public static void Save()
        {
            if (string.IsNullOrEmpty(path))
            {
                path = Path.Combine(AppContext.BaseDirectory, "spec.json");
            }

            if (Spec == null)
            {
                Spec = new VoltageSpec();
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            File.WriteAllText(path, JsonSerializer.Serialize(Spec, options));
            Changed?.Invoke(null, EventArgs.Empty);
        }
    }
}
