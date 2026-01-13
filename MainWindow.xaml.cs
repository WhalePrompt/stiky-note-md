using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Markdig;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace StickyNoteMD
{
    public partial class MainWindow : Window
    {
        private bool _isPreviewMode = false;
        private readonly MarkdownPipeline _markdownPipeline;
        private readonly string _noteId;
        private readonly string _noteFilePath;
        private readonly string _colorFilePath;
        private readonly string _positionFilePath;
        private readonly string _notesFolder;
        private bool _isLoading = false;
        private string _currentTitleColor = "#B3E5FC";
        private string _currentBgColor = "#E1F5FE";

        public MainWindow() : this(Guid.NewGuid().ToString())
        {
        }

        public MainWindow(string noteId)
        {
            InitializeComponent();

            _noteId = noteId;

            _markdownPipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()
                .Build();

            _notesFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "StickyNoteMD",
                "notes"
            );

            _noteFilePath = Path.Combine(_notesFolder, $"{_noteId}.md");
            _colorFilePath = Path.Combine(_notesFolder, $"{_noteId}.color");
            _positionFilePath = Path.Combine(_notesFolder, $"{_noteId}.position");

            LoadNote();
            LoadColor();
            LoadPosition();
            InitializeWebView();

            this.KeyDown += MainWindow_KeyDown;
            this.Loaded += (s, e) => UpdatePinButton();
        }

        private async void InitializeWebView()
        {
            try
            {
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "StickyNoteMD",
                    "WebView2"
                );
                Directory.CreateDirectory(userDataFolder);

                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
                await MarkdownPreview.EnsureCoreWebView2Async(env);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView2 initialization failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Markdown Conversion

        private void LoadNote()
        {
            _isLoading = true;
            try
            {
                if (File.Exists(_noteFilePath))
                {
                    var markdown = File.ReadAllText(_noteFilePath);
                    MarkdownToRichTextBox(markdown);
                }
            }
            catch { }
            finally
            {
                _isLoading = false;
            }
        }

        private void SaveNote()
        {
            if (_isLoading) return;

            try
            {
                Directory.CreateDirectory(_notesFolder);
                var markdown = RichTextBoxToMarkdown();
                File.WriteAllText(_noteFilePath, markdown);
            }
            catch { }
        }

        private void LoadColor()
        {
            try
            {
                if (File.Exists(_colorFilePath))
                {
                    var colorData = File.ReadAllText(_colorFilePath).Split(',');
                    if (colorData.Length == 2)
                    {
                        _currentTitleColor = colorData[0];
                        _currentBgColor = colorData[1];
                        ApplyColor();
                    }
                }
            }
            catch { }
        }

        private void SaveColor()
        {
            try
            {
                Directory.CreateDirectory(_notesFolder);
                File.WriteAllText(_colorFilePath, $"{_currentTitleColor},{_currentBgColor}");
            }
            catch { }
        }

        private void LoadPosition()
        {
            try
            {
                if (File.Exists(_positionFilePath))
                {
                    var data = File.ReadAllText(_positionFilePath).Split(',');
                    if (data.Length == 4)
                    {
                        var culture = System.Globalization.CultureInfo.InvariantCulture;
                        this.Left = double.Parse(data[0], culture);
                        this.Top = double.Parse(data[1], culture);
                        this.Width = double.Parse(data[2], culture);
                        this.Height = double.Parse(data[3], culture);
                    }
                }
            }
            catch { }
        }

        private void SavePosition()
        {
            try
            {
                Directory.CreateDirectory(_notesFolder);
                var culture = System.Globalization.CultureInfo.InvariantCulture;
                var posData = string.Format(culture, "{0},{1},{2},{3}", this.Left, this.Top, this.Width, this.Height);
                File.WriteAllText(_positionFilePath, posData);
            }
            catch { }
        }

        private void ApplyColor()
        {
            var brush = new BrushConverter();
            var titleBar = ((Grid)((Border)this.Content).Child).Children[0] as Border;

            if (titleBar != null)
                titleBar.Background = brush.ConvertFromString(_currentTitleColor) as Brush;

            ToolbarBorder.Background = brush.ConvertFromString(_currentBgColor) as Brush;
            NoteRichTextBox.Background = brush.ConvertFromString(_currentBgColor) as Brush;
        }

        private void MarkdownToRichTextBox(string markdown)
        {
            var doc = NoteRichTextBox.Document;
            doc.Blocks.Clear();

            if (string.IsNullOrEmpty(markdown))
            {
                doc.Blocks.Add(new Paragraph { Margin = new Thickness(0) });
                return;
            }

            var lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                var paragraph = new Paragraph { Margin = new Thickness(0) };
                ParseInlineMarkdown(line, paragraph.Inlines);
                doc.Blocks.Add(paragraph);
            }
        }

        private void ParseInlineMarkdown(string text, InlineCollection inlines)
        {
            // Pattern for bold (**text** or __text__)
            // Pattern for italic (*text* or _text_)
            // Pattern for strikethrough (~~text~~)
            // Pattern for underline (<u>text</u>)

            var pattern = @"(\*\*|__)(.+?)\1|(\*|_)(.+?)\3|~~(.+?)~~|<u>(.+?)</u>";
            var regex = new Regex(pattern);

            int lastIndex = 0;
            var matches = regex.Matches(text);

            foreach (Match match in matches)
            {
                // Add text before match
                if (match.Index > lastIndex)
                {
                    inlines.Add(new Run(text.Substring(lastIndex, match.Index - lastIndex)));
                }

                // Bold
                if (match.Groups[2].Success)
                {
                    var run = new Run(match.Groups[2].Value) { FontWeight = FontWeights.Bold };
                    inlines.Add(run);
                }
                // Italic
                else if (match.Groups[4].Success)
                {
                    var run = new Run(match.Groups[4].Value) { FontStyle = FontStyles.Italic };
                    inlines.Add(run);
                }
                // Strikethrough
                else if (match.Groups[5].Success)
                {
                    var run = new Run(match.Groups[5].Value);
                    run.TextDecorations = TextDecorations.Strikethrough;
                    inlines.Add(run);
                }
                // Underline
                else if (match.Groups[6].Success)
                {
                    var run = new Run(match.Groups[6].Value);
                    run.TextDecorations = TextDecorations.Underline;
                    inlines.Add(run);
                }

                lastIndex = match.Index + match.Length;
            }

            // Add remaining text
            if (lastIndex < text.Length)
            {
                inlines.Add(new Run(text.Substring(lastIndex)));
            }

            // If no content, add empty run
            if (inlines.Count == 0)
            {
                inlines.Add(new Run());
            }
        }

        private string RichTextBoxToMarkdown()
        {
            var sb = new StringBuilder();
            var doc = NoteRichTextBox.Document;

            foreach (var block in doc.Blocks)
            {
                if (block is Paragraph paragraph)
                {
                    var line = InlinesToMarkdown(paragraph.Inlines);
                    sb.AppendLine(line);
                }
                else if (block is List list)
                {
                    foreach (var listItem in list.ListItems)
                    {
                        foreach (var itemBlock in listItem.Blocks)
                        {
                            if (itemBlock is Paragraph itemPara)
                            {
                                sb.AppendLine("- " + InlinesToMarkdown(itemPara.Inlines));
                            }
                        }
                    }
                }
            }

            return sb.ToString().TrimEnd();
        }

        private string GetPlainText(InlineCollection inlines)
        {
            var sb = new StringBuilder();
            foreach (var inline in inlines)
            {
                if (inline is Run run)
                    sb.Append(run.Text);
                else if (inline is Span span)
                    sb.Append(GetPlainText(span.Inlines));
            }
            return sb.ToString();
        }

        private bool IsMarkdownLine(string text)
        {
            var trimmed = text.TrimStart();

            // Heading: #, ##, ###, etc.
            if (trimmed.StartsWith("#")) return true;

            // List: -, *, +
            if (trimmed.StartsWith("- ") || trimmed.StartsWith("* ") || trimmed.StartsWith("+ ")) return true;

            // Numbered list: 1., 2., etc.
            if (Regex.IsMatch(trimmed, @"^\d+\.\s")) return true;

            // Blockquote: >
            if (trimmed.StartsWith(">")) return true;

            // Code block: ```
            if (trimmed.StartsWith("```")) return true;

            // Horizontal rule: ---, ***, ___
            if (trimmed.StartsWith("---") || trimmed.StartsWith("***") || trimmed.StartsWith("___")) return true;

            return false;
        }

        private string InlinesToMarkdown(InlineCollection inlines)
        {
            var plainText = GetPlainText(inlines);

            // If markdown line, return plain text without formatting
            if (IsMarkdownLine(plainText))
            {
                return plainText;
            }

            var sb = new StringBuilder();
            ProcessInlines(inlines, sb, FontWeights.Normal, FontStyles.Normal, false, false);
            return sb.ToString();
        }

        private void ProcessInlines(InlineCollection inlines, StringBuilder sb,
            FontWeight parentWeight, FontStyle parentStyle, bool parentStrikethrough, bool parentUnderline)
        {
            foreach (var inline in inlines)
            {
                if (inline is Run run)
                {
                    var text = run.Text;
                    if (string.IsNullOrEmpty(text)) continue;

                    // Check formatting - inherit from parent if not set
                    bool isBold = run.FontWeight == FontWeights.Bold || parentWeight == FontWeights.Bold;
                    bool isItalic = run.FontStyle == FontStyles.Italic || parentStyle == FontStyles.Italic;

                    bool isStrikethrough = parentStrikethrough;
                    bool isUnderline = parentUnderline;

                    if (run.TextDecorations != null)
                    {
                        foreach (var dec in run.TextDecorations)
                        {
                            if (dec.Location == TextDecorationLocation.Strikethrough)
                                isStrikethrough = true;
                            if (dec.Location == TextDecorationLocation.Underline)
                                isUnderline = true;
                        }
                    }

                    if (isBold) sb.Append("**");
                    if (isItalic) sb.Append("*");
                    if (isStrikethrough) sb.Append("~~");
                    if (isUnderline) sb.Append("<u>");

                    sb.Append(text);

                    if (isUnderline) sb.Append("</u>");
                    if (isStrikethrough) sb.Append("~~");
                    if (isItalic) sb.Append("*");
                    if (isBold) sb.Append("**");
                }
                else if (inline is Span span)
                {
                    // Get span's formatting
                    var spanWeight = span.FontWeight;
                    var spanStyle = span.FontStyle;
                    bool spanStrikethrough = parentStrikethrough;
                    bool spanUnderline = parentUnderline;

                    if (span.TextDecorations != null)
                    {
                        foreach (var dec in span.TextDecorations)
                        {
                            if (dec.Location == TextDecorationLocation.Strikethrough)
                                spanStrikethrough = true;
                            if (dec.Location == TextDecorationLocation.Underline)
                                spanUnderline = true;
                        }
                    }

                    ProcessInlines(span.Inlines, sb, spanWeight, spanStyle, spanStrikethrough, spanUnderline);
                }
            }
        }

        #endregion

        #region Event Handlers

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                switch (e.Key)
                {
                    case Key.B:
                        Bold_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.I:
                        Italic_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.U:
                        Underline_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.P:
                        Preview_Click(sender, e);
                        e.Handled = true;
                        break;
                    case Key.S:
                        SaveNote();
                        e.Handled = true;
                        break;
                }
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void AddNote_Click(object sender, RoutedEventArgs e)
        {
            var newWindow = new MainWindow();
            newWindow.Show();
        }

        private void Pin_Click(object sender, RoutedEventArgs e)
        {
            this.Topmost = !this.Topmost;
            UpdatePinButton();
        }

        private void UpdatePinButton()
        {
            PinHead.Fill = this.Topmost
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A1A"))
                : Brushes.Transparent;
        }

        private void ColorPalette_Click(object sender, RoutedEventArgs e)
        {
            var colors = new[] {
                ("#FFF59D", "#FFFDE7"),
                ("#C8E6C9", "#E8F5E9"),
                ("#F8BBD0", "#FCE4EC"),
                ("#E1BEE7", "#F3E5F5"),
                ("#B3E5FC", "#E1F5FE"),
                ("#E0E0E0", "#F5F5F5"),
                ("#9E9E9E", "#BDBDBD")
            };

            var popup = new Popup
            {
                StaysOpen = false,
                Placement = PlacementMode.Bottom,
                PlacementTarget = sender as Button
            };

            var border = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E0E0E0")),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(2)
            };

            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };

            foreach (var (titleColor, bgColor) in colors)
            {
                var tc = titleColor;
                var bc = bgColor;

                var colorBtn = new Button
                {
                    Width = 60,
                    Height = 60,
                    Margin = new Thickness(0),
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bgColor)),
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(titleColor)),
                    BorderThickness = new Thickness(1),
                    Cursor = Cursors.Hand
                };

                colorBtn.Click += (s, args) =>
                {
                    ChangeNoteColor(tc, bc);
                    popup.IsOpen = false;
                };

                stackPanel.Children.Add(colorBtn);
            }

            border.Child = stackPanel;
            popup.Child = border;
            popup.IsOpen = true;
        }

        private void Menu_Click(object sender, RoutedEventArgs e)
        {
            var contextMenu = new ContextMenu();

            var saveItem = new MenuItem { Header = "Save (_S)" };
            saveItem.Click += (s, args) => SaveNote();

            var deleteItem = new MenuItem { Header = "Delete Note (_D)" };
            deleteItem.Click += (s, args) =>
            {
                var result = MessageBox.Show("Are you sure you want to delete this note?", "Delete Note",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    DeleteNoteFile();
                    this.Close();
                }
            };

            var alwaysOnTop = new MenuItem
            {
                Header = "Always on Top (_T)",
                IsCheckable = true,
                IsChecked = this.Topmost
            };
            alwaysOnTop.Click += (s, args) =>
            {
                this.Topmost = alwaysOnTop.IsChecked;
                UpdatePinButton();
            };

            contextMenu.Items.Add(saveItem);
            contextMenu.Items.Add(deleteItem);
            contextMenu.Items.Add(new Separator());
            contextMenu.Items.Add(alwaysOnTop);

            contextMenu.IsOpen = true;
        }

        private void DeleteNoteFile()
        {
            try
            {
                if (File.Exists(_noteFilePath))
                    File.Delete(_noteFilePath);
                if (File.Exists(_colorFilePath))
                    File.Delete(_colorFilePath);
                if (File.Exists(_positionFilePath))
                    File.Delete(_positionFilePath);
            }
            catch { }
        }

        private void ChangeNoteColor(string titleColor, string toolbarColor)
        {
            _currentTitleColor = titleColor;
            _currentBgColor = toolbarColor;
            ApplyColor();
            SaveColor();
        }

        private void NoteRichTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            ToolbarBorder.Visibility = Visibility.Visible;
        }

        private void NoteRichTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ToolbarBorder.Visibility = Visibility.Collapsed;
        }

        private void NoteRichTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SaveNote();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            SaveNote();
            this.Close();
        }

        #endregion

        #region Formatting

        private void ApplyFormatting(DependencyProperty property, object value)
        {
            var selection = NoteRichTextBox.Selection;
            if (selection.IsEmpty)
            {
                NoteRichTextBox.Focus();
                return;
            }

            var currentValue = selection.GetPropertyValue(property);

            if (property == TextElement.FontWeightProperty)
            {
                var newValue = currentValue.Equals(FontWeights.Bold) ? FontWeights.Normal : FontWeights.Bold;
                selection.ApplyPropertyValue(property, newValue);
            }
            else if (property == TextElement.FontStyleProperty)
            {
                var newValue = currentValue.Equals(FontStyles.Italic) ? FontStyles.Normal : FontStyles.Italic;
                selection.ApplyPropertyValue(property, newValue);
            }
            else if (property == Inline.TextDecorationsProperty)
            {
                var decorations = currentValue as TextDecorationCollection;
                var targetDecoration = value as TextDecorationCollection;

                if (decorations != null && targetDecoration != null && decorations.Count > 0)
                {
                    bool hasDecoration = false;
                    foreach (var dec in targetDecoration)
                    {
                        if (decorations.Contains(dec))
                        {
                            hasDecoration = true;
                            break;
                        }
                    }
                    selection.ApplyPropertyValue(property, hasDecoration ? null : value);
                }
                else
                {
                    selection.ApplyPropertyValue(property, value);
                }
            }

            NoteRichTextBox.Focus();
        }

        private void Bold_Click(object sender, RoutedEventArgs e)
        {
            ApplyFormatting(TextElement.FontWeightProperty, FontWeights.Bold);
        }

        private void Italic_Click(object sender, RoutedEventArgs e)
        {
            ApplyFormatting(TextElement.FontStyleProperty, FontStyles.Italic);
        }

        private void Underline_Click(object sender, RoutedEventArgs e)
        {
            ApplyFormatting(Inline.TextDecorationsProperty, TextDecorations.Underline);
        }

        private void Strikethrough_Click(object sender, RoutedEventArgs e)
        {
            ApplyFormatting(Inline.TextDecorationsProperty, TextDecorations.Strikethrough);
        }

        private void List_Click(object sender, RoutedEventArgs e)
        {
            var selection = NoteRichTextBox.Selection;

            if (selection.Start.Paragraph != null)
            {
                var paragraph = selection.Start.Paragraph;
                var list = new List { Margin = new Thickness(0) };
                var listItem = new ListItem(new Paragraph { Margin = new Thickness(0) });

                // Copy content from paragraph to list item
                var listItemPara = (Paragraph)listItem.Blocks.FirstBlock;
                while (paragraph.Inlines.Count > 0)
                {
                    var inline = paragraph.Inlines.FirstInline;
                    paragraph.Inlines.Remove(inline);
                    listItemPara.Inlines.Add(inline);
                }

                list.ListItems.Add(listItem);

                var doc = NoteRichTextBox.Document;
                var index = doc.Blocks.ToList().IndexOf(paragraph);
                doc.Blocks.Remove(paragraph);

                if (index >= 0 && index < doc.Blocks.Count)
                {
                    doc.Blocks.InsertBefore(doc.Blocks.ElementAt(index), list);
                }
                else
                {
                    doc.Blocks.Add(list);
                }
            }

            NoteRichTextBox.Focus();
        }

        private void Image_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.gif;*.bmp|All Files|*.*",
                Title = "Select Image"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var image = new System.Windows.Controls.Image();
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage(new Uri(dialog.FileName));
                    image.Source = bitmap;
                    image.MaxWidth = 200;

                    var container = new InlineUIContainer(image);

                    if (NoteRichTextBox.Selection.Start.Paragraph != null)
                    {
                        NoteRichTextBox.Selection.Start.Paragraph.Inlines.Add(container);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Cannot load image: {ex.Message}");
                }
            }

            NoteRichTextBox.Focus();
        }

        #endregion

        #region Preview

        private string GetCurrentBackgroundColorHex()
        {
            if (NoteRichTextBox.Background is SolidColorBrush brush)
            {
                var color = brush.Color;
                return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }
            return "#E1F5FE";
        }

        private void Preview_Click(object sender, RoutedEventArgs e)
        {
            _isPreviewMode = !_isPreviewMode;

            if (_isPreviewMode)
            {
                NoteRichTextBox.Visibility = Visibility.Collapsed;
                MarkdownPreview.Visibility = Visibility.Visible;

                var markdown = RichTextBoxToMarkdown();
                var bgColor = GetCurrentBackgroundColorHex();
                var html = Markdig.Markdown.ToHtml(markdown, _markdownPipeline);

                var fullHtml = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{
            font-family: 'Segoe UI', sans-serif;
            font-size: 14px;
            padding: 12px;
            margin: 0;
            line-height: 1.5;
            color: #1a1a1a;
            background-color: {bgColor};
            word-wrap: break-word;
            overflow-wrap: break-word;
        }}
        h1, h2, h3, h4, h5, h6 {{ margin-top: 0.5em; margin-bottom: 0.3em; }}
        p {{ margin: 0.5em 0; }}
        code {{ background: #f0f0f0; padding: 2px 5px; border-radius: 3px; font-family: Consolas, monospace; }}
        pre {{ background: #f0f0f0; padding: 10px; border-radius: 5px; white-space: pre-wrap; word-wrap: break-word; }}
        pre code {{ padding: 0; background: none; }}
        blockquote {{ margin: 0.5em 0; padding-left: 1em; border-left: 3px solid #80D4F7; color: #555; }}
        ul, ol {{ margin: 0.5em 0; padding-left: 1.5em; }}
        img {{ max-width: 100%; height: auto; }}
        a {{ color: #0066cc; }}
        table {{ border-collapse: collapse; margin: 0.5em 0; }}
        th, td {{ border: 1px solid #ddd; padding: 6px 10px; }}
        th {{ background: #f5f5f5; }}
    </style>
</head>
<body>
{html}
</body>
</html>";

                MarkdownPreview.NavigateToString(fullHtml);
            }
            else
            {
                MarkdownPreview.Visibility = Visibility.Collapsed;
                NoteRichTextBox.Visibility = Visibility.Visible;
                NoteRichTextBox.Focus();
            }
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            SaveNote();
            SavePosition();
            base.OnClosed(e);
        }
    }

    public static class BlockCollectionExtensions
    {
        public static System.Collections.Generic.List<Block> ToList(this BlockCollection blocks)
        {
            var list = new System.Collections.Generic.List<Block>();
            foreach (var block in blocks)
                list.Add(block);
            return list;
        }

        public static Block ElementAt(this BlockCollection blocks, int index)
        {
            int i = 0;
            foreach (var block in blocks)
            {
                if (i == index) return block;
                i++;
            }
            return null;
        }
    }
}
