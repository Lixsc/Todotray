using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace IDK
{
    public class TodoRepository
    {
        private readonly string _filePath;
        private readonly JavaScriptSerializer _serializer;

        public string StoragePath => _filePath;

        public TodoRepository()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string folder = Path.Combine(appData, "TodoTray");
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            _filePath = Path.Combine(folder, "todos.json");

            // Migrate from legacy folder if present
            string oldFolder = Path.Combine(appData, "IDK_TodoApp");
            string oldFile = Path.Combine(oldFolder, "todos.json");
            if (!File.Exists(_filePath) && File.Exists(oldFile))
            {
                try
                {
                    File.Copy(oldFile, _filePath, true);
                }
                catch { }
            }

            _serializer = new JavaScriptSerializer();
        }

        public List<TodoItem> Load()
        {
            if (!File.Exists(_filePath))
            {
                return new List<TodoItem>();
            }

            try
            {
                string json = File.ReadAllText(_filePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return new List<TodoItem>();
                }

                var items = _serializer.Deserialize<List<TodoItem>>(json);
                return items ?? new List<TodoItem>();
            }
            catch (Exception)
            {
                return new List<TodoItem>();
            }
        }

        public void Save(IEnumerable<TodoItem> items)
        {
            try
            {
                string json = _serializer.Serialize(items);
                File.WriteAllText(_filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Failed to save TodoTray todos: " + ex.Message);
            }
        }
    }
}

