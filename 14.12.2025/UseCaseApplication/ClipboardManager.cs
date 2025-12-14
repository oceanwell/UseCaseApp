using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;
using System.Windows.Markup;
using System.IO;
using System.Xml;

namespace UseCaseApplication
{
    public static class ClipboardManager
    {
        private static string _clipboardXaml;
        private static Dictionary<string, object> _clipboardMetadata;

        public static void Copy(UIElement element)
        {
            if (element == null) return;
            try
            {
                _clipboardXaml = XamlWriter.Save(element);
                _clipboardMetadata = new Dictionary<string, object>();
                
                if (element is FrameworkElement fe)
                {
                    _clipboardMetadata["Width"] = fe.Width;
                    _clipboardMetadata["Height"] = fe.Height;
                    _clipboardMetadata["Tag"] = fe.Tag;
                    
                    if (element is Shape shape)
                    {
                        _clipboardMetadata["StrokeThickness"] = shape.StrokeThickness;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при копировании: {ex.Message}");
            }
        }

        public static UIElement Paste(Point location)
        {
            if (string.IsNullOrEmpty(_clipboardXaml)) return null;

            try
            {
                using (var stringReader = new StringReader(_clipboardXaml))
                using (var xmlReader = XmlReader.Create(stringReader))
                {
                    var element = (UIElement)XamlReader.Load(xmlReader);

                    if (element is FrameworkElement fe)
                    {
                        if (_clipboardMetadata != null)
                        {
                            if (_clipboardMetadata.ContainsKey("Tag")) fe.Tag = _clipboardMetadata["Tag"];
                        }
                        
                        Canvas.SetLeft(fe, location.X);
                        Canvas.SetTop(fe, location.Y);
                    }

                    return element;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при вставке: {ex.Message}");
                return null;
            }
        }

        public static bool HasContent()
        {
            return !string.IsNullOrEmpty(_clipboardXaml);
        }
    }
}
