using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace StickyNoteMD
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var notesFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StickyNoteMD",
                "notes"
            );

            bool openedAny = false;

            // Load existing notes
            if (Directory.Exists(notesFolder))
            {
                var noteFiles = Directory.GetFiles(notesFolder, "*.md");

                foreach (var file in noteFiles)
                {
                    var noteId = Path.GetFileNameWithoutExtension(file);

                    // Skip empty files
                    var content = File.ReadAllText(file);
                    if (string.IsNullOrWhiteSpace(content))
                    {
                        File.Delete(file);
                        continue;
                    }

                    var window = new MainWindow(noteId);
                    window.Show();
                    openedAny = true;
                }
            }

            // Create new note if no notes exist
            if (!openedAny)
            {
                var window = new MainWindow();
                window.Show();
            }
        }
    }
}
