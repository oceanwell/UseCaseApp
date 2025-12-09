using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace UseCaseApplication
{
    public partial class MainWindow : Window
    {
        private readonly Stack<UIElement> undoStack = new Stack<UIElement>();
        private readonly Stack<UIElement> redoStack = new Stack<UIElement>();
        private double currentLineThickness = 2.0;
        
        private Point dragStartPoint;
        private Button sourceButton;
        private bool draggingFromPanel;
        
        private UIElement selectedElement;
        private List<UIElement> selectedElements = new List<UIElement>();
        private Point moveStartPoint;
        private bool movingElement;
        private double originalLeft;
        private double originalTop;
        
        private bool movingCanvas;
        private Point canvasMoveStartPoint;
        

        public MainWindow()
        {
            InitializeComponent();
            
            ThicknessText.Text = currentLineThickness.ToString();
        }

        private void WindowHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Maximize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ToolButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                sourceButton = button;
                dragStartPoint = e.GetPosition(button);
                draggingFromPanel = false;
            }
        }

        private void ToolButton_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sourceButton != null && !draggingFromPanel)
            {
                var currentPosition = e.GetPosition(sourceButton);
                
                if (Math.Abs(currentPosition.X - dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(currentPosition.Y - dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    string instrument = sourceButton.Tag as string ?? string.Empty;
                    if (!string.IsNullOrEmpty(instrument))
                    {
                        draggingFromPanel = true;
                        DragDrop.DoDragDrop(sourceButton, instrument, DragDropEffects.Copy);
                        draggingFromPanel = false;
                    }
                    sourceButton = null;
                }
            }
        }

        private void ZoomSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (ScaleTransform == null || ZoomLabel == null) return;
            var scale = e.NewValue / 100.0;
            
            var animationX = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = scale,
                Duration = TimeSpan.FromMilliseconds(50),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            
            var animationY = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = scale,
                Duration = TimeSpan.FromMilliseconds(50),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            
            ScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animationX);
            ScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animationY);
            ZoomLabel.Text = $"{(int)e.NewValue}%";
        }

        private void GridToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (GridBackground == null) return;
            
            if (GridToggle.IsChecked == true)
            {
                GridBackground.Visibility = Visibility.Visible;
            }
            else
            {
                GridBackground.Visibility = Visibility.Hidden;
            }
        }

        private void DecreaseThickness_Click(object sender, RoutedEventArgs e)
        {
            if (currentLineThickness > 1)
            {
                currentLineThickness--;
                ThicknessText.Text = currentLineThickness.ToString();
                UpdateLineThickness();
            }
        }

        private void IncreaseThickness_Click(object sender, RoutedEventArgs e)
        {
            if (currentLineThickness < 10)
            {
                currentLineThickness++;
                ThicknessText.Text = currentLineThickness.ToString();
                UpdateLineThickness();
            }
        }

        private void UpdateLineThickness()
        {
            if (DrawingCanvas == null) return;
            foreach (var element in DrawingCanvas.Children.OfType<Shape>())
            {
                element.StrokeThickness = currentLineThickness;
            }
        }

        private void DrawingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (draggingFromPanel)
            {
                return;
            }
            
            var element = e.OriginalSource as UIElement;
            
            if (element == DrawingCanvas || element == GridBackground)
            {
                ClearSelection();
                movingCanvas = true;
                canvasMoveStartPoint = e.GetPosition(this);
                Mouse.Capture(DrawingCanvas);
                DrawingCanvas.Cursor = Cursors.Hand;
                return;
            }
            
            var parentElement = FindElementOnCanvas(element);
            
            if (parentElement != null && DrawingCanvas.Children.Contains(parentElement))
            {
                bool shiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
                
                if (!shiftPressed)
                {
                    ClearSelection();
                }
                
                selectedElement = parentElement;
                movingElement = true;
                moveStartPoint = e.GetPosition(DrawingCanvas);
                
                var currentLeft = Canvas.GetLeft(selectedElement);
                var currentTop = Canvas.GetTop(selectedElement);
                originalLeft = double.IsNaN(currentLeft) ? 0 : currentLeft;
                originalTop = double.IsNaN(currentTop) ? 0 : currentTop;
                
                Mouse.Capture(DrawingCanvas);
                
                if (!selectedElements.Contains(selectedElement))
                {
                    selectedElements.Add(selectedElement);
                }
                
                HighlightElement(selectedElement);
            }
        }
        
        private void HighlightElement(UIElement element)
        {
            if (element is Shape shape)
            {
                shape.Stroke = Brushes.DodgerBlue;
                shape.StrokeThickness = 2;
            }
            else if (element is Canvas canvas)
            {
                foreach (var child in canvas.Children.OfType<Shape>())
                {
                    child.Stroke = Brushes.DodgerBlue;
                    child.StrokeThickness = 2;
                }
            }
        }
        
        private void ClearSelection()
        {
            foreach (var element in selectedElements.ToList())
            {
                if (element is Shape shape)
                {
                    shape.Stroke = Brushes.Black;
                    shape.StrokeThickness = currentLineThickness;
                }
                else if (element is Canvas canvas)
                {
                    foreach (var child in canvas.Children.OfType<Shape>())
                    {
                        child.Stroke = Brushes.Black;
                        child.StrokeThickness = currentLineThickness;
                    }
                }
            }
            selectedElements.Clear();
        }
        
        private void DrawingCanvas_DragOver(object sender, DragEventArgs e)
        {
            if (movingElement || movingCanvas)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }
            
            if (draggingFromPanel && e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void DrawingCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (movingCanvas)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    var currentPos = e.GetPosition(this);
                    var deltaX = currentPos.X - canvasMoveStartPoint.X;
                    var deltaY = currentPos.Y - canvasMoveStartPoint.Y;
                    
                    TranslateTransform.X += deltaX;
                    TranslateTransform.Y += deltaY;
                    
                    canvasMoveStartPoint = currentPos;
                }
                else
                {
                    movingCanvas = false;
                    Mouse.Capture(null);
                    DrawingCanvas.Cursor = Cursors.Arrow;
                }
                return;
            }
            
            if (movingElement && selectedElement != null)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    var currentPos = e.GetPosition(DrawingCanvas);
                    
                    var offsetX = currentPos.X - moveStartPoint.X;
                    var offsetY = currentPos.Y - moveStartPoint.Y;
                    
                    Canvas.SetLeft(selectedElement, originalLeft + offsetX);
                    Canvas.SetTop(selectedElement, originalTop + offsetY);
                }
                else
                {
                    movingElement = false;
                    Mouse.Capture(null);
                    selectedElement = null;
                }
            }
        }

        private void DrawingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (movingCanvas)
            {
                movingCanvas = false;
                Mouse.Capture(null);
                DrawingCanvas.Cursor = Cursors.Arrow;
            }
            
            if (movingElement)
            {
                movingElement = false;
                Mouse.Capture(null);
                selectedElement = null;
            }
        }

        private void DrawingCanvas_Drop(object sender, DragEventArgs e)
        {
            if (movingElement || movingCanvas || !draggingFromPanel)
            {
                e.Handled = true;
                return;
            }
            
            if (!e.Data.GetDataPresent(DataFormats.StringFormat)) return;
            
            var instrument = (string)e.Data.GetData(DataFormats.StringFormat);
            var dropPoint = e.GetPosition(DrawingCanvas);

            UIElement element = CreateElementByTool(instrument, dropPoint);
            if (element != null)
            {
                AddToCanvas(element);
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            if (DrawingCanvas.Children.Count == 0) return;
            var element = DrawingCanvas.Children[DrawingCanvas.Children.Count - 1] as UIElement;
            DrawingCanvas.Children.RemoveAt(DrawingCanvas.Children.Count - 1);
            undoStack.Push(element);
            redoStack.Clear();
        }

        private void Redo_Click(object sender, RoutedEventArgs e)
        {
            if (undoStack.Count == 0) return;
            var element = undoStack.Pop();
            DrawingCanvas.Children.Add(element);
            redoStack.Push(element);
        }

        private void AddToCanvas(UIElement element)
        {
            DrawingCanvas.Children.Add(element);
            redoStack.Clear();
        }

        private UIElement FindElementOnCanvas(UIElement element)
        {
            var current = element;
            while (current != null && current != DrawingCanvas)
            {
                var parent = VisualTreeHelper.GetParent(current) as UIElement;
                if (parent == DrawingCanvas)
                {
                    return current;
                }
                current = parent;
            }
            return null;
        }

        private UIElement CreateActor()
        {
            var group = new Canvas();

            var head = new Ellipse { Width = 30, Height = 30, Stroke = Brushes.Black, StrokeThickness = currentLineThickness, Fill = Brushes.Black };
            Canvas.SetLeft(head, 50);
            Canvas.SetTop(head, 30);

            var body = new Line { X1 = 65, Y1 = 60, X2 = 65, Y2 = 120, Stroke = Brushes.Black, StrokeThickness = currentLineThickness };
            var leftArm = new Line { X1 = 35, Y1 = 80, X2 = 95, Y2 = 80, Stroke = Brushes.Black, StrokeThickness = currentLineThickness };
            var leftLeg = new Line { X1 = 65, Y1 = 120, X2 = 45, Y2 = 150, Stroke = Brushes.Black, StrokeThickness = currentLineThickness };
            var rightLeg = new Line { X1 = 65, Y1 = 120, X2 = 85, Y2 = 150, Stroke = Brushes.Black, StrokeThickness = currentLineThickness };

            group.Children.Add(head);
            group.Children.Add(body);
            group.Children.Add(leftArm);
            group.Children.Add(leftLeg);
            group.Children.Add(rightLeg);

            Canvas.SetLeft(group, 0);
            Canvas.SetTop(group, 0);
            return group;
        }

        private UIElement CreateElementByTool(string instrument, Point point)
        {
            switch (instrument)
            {
                case "aktor":
                {
                    var actor = CreateActor();
                    Canvas.SetLeft(actor, point.X - 65);
                    Canvas.SetTop(actor, point.Y - 90);
                    return actor;
                }
                case "pretsedent":
                {
                    var ellipse = new Ellipse
                    {
                        Width = 120,
                        Height = 60,
                        Stroke = Brushes.Black,
                        StrokeThickness = currentLineThickness,
                        Fill = Brushes.White
                    };
                    Canvas.SetLeft(ellipse, point.X - 60);
                    Canvas.SetTop(ellipse, point.Y - 30);
                    return ellipse;
                }
                case "sistema":
                {
                    var rectangle = new Rectangle
                    {
                        Width = 240,
                        Height = 160,
                        Stroke = Brushes.Black,
                        StrokeThickness = currentLineThickness,
                        Fill = Brushes.Transparent,
                        RadiusX = 4,
                        RadiusY = 4
                    };
                    Canvas.SetLeft(rectangle, point.X - 120);
                    Canvas.SetTop(rectangle, point.Y - 80);
                    return rectangle;
                }
                case "liniya":
                {
                    var line = new Line
                    {
                        X1 = point.X,
                        Y1 = point.Y,
                        X2 = point.X + 120,
                        Y2 = point.Y + 60,
                        Stroke = Brushes.Black,
                        StrokeThickness = currentLineThickness
                    };
                    return line;
                }
                case "vklyuchit":
                {
                    var group = new Canvas();
                    var line = new Line
                    {
                        X1 = 0,
                        Y1 = 20,
                        X2 = 130,
                        Y2 = 20,
                        Stroke = Brushes.Black,
                        StrokeThickness = currentLineThickness,
                        StrokeDashArray = new DoubleCollection { 5, 3 }
                    };
                    var arrow = new System.Windows.Shapes.Polygon
                    {
                        Points = new PointCollection { new Point(140, 20), new Point(130, 16), new Point(130, 24) },
                        Fill = Brushes.Black
                    };
                    var text = new TextBlock { Text = "<<include>>", Background = Brushes.LightYellow, FontSize = 11 };
                    Canvas.SetLeft(text, 45);
                    Canvas.SetTop(text, 2);
                    group.Children.Add(line);
                    group.Children.Add(arrow);
                    group.Children.Add(text);
                    Canvas.SetLeft(group, point.X - 70);
                    Canvas.SetTop(group, point.Y - 20);
                    return group;
                }
                case "rasshirit":
                {
                    var group = new Canvas();
                    var line = new Line
                    {
                        X1 = 0,
                        Y1 = 20,
                        X2 = 130,
                        Y2 = 20,
                        Stroke = Brushes.Black,
                        StrokeThickness = currentLineThickness,
                        StrokeDashArray = new DoubleCollection { 5, 3 }
                    };
                    var arrow = new System.Windows.Shapes.Polygon
                    {
                        Points = new PointCollection { new Point(140, 20), new Point(130, 16), new Point(130, 24) },
                        Fill = Brushes.Black
                    };
                    var text = new TextBlock { Text = "<<extend>>", Background = Brushes.LightYellow, FontSize = 11 };
                    Canvas.SetLeft(text, 45);
                    Canvas.SetTop(text, 2);
                    group.Children.Add(line);
                    group.Children.Add(arrow);
                    group.Children.Add(text);
                    Canvas.SetLeft(group, point.X - 70);
                    Canvas.SetTop(group, point.Y - 20);
                    return group;
                }
                case "obobshenie":
                {
                    var path = new Path
                    {
                        Stroke = Brushes.Black,
                        StrokeThickness = currentLineThickness,
                        Fill = Brushes.White,
                        Data = Geometry.Parse("M 0 20 L 100 20 L 100 10 L 120 20 L 100 30 L 100 20")
                    };
                    Canvas.SetLeft(path, point.X - 60);
                    Canvas.SetTop(path, point.Y - 20);
                    return path;
                }
                case "tekst":
                {
                    var textBlock = new TextBlock { Text = "Текст", FontSize = 16, Foreground = Brushes.Black };
                    Canvas.SetLeft(textBlock, point.X - 20);
                    Canvas.SetTop(textBlock, point.Y - 10);
                    return textBlock;
                }
                default:
                    return null;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Открываем Page3 в новом окне при нажатии на кнопку Помощь
            Window helpWindow = new Window();
            helpWindow.Title = "Помощь";
            helpWindow.Width = 900;
            helpWindow.Height = 600;
            helpWindow.WindowStyle = WindowStyle.None;
            helpWindow.AllowsTransparency = true;
            helpWindow.Background = new SolidColorBrush(Color.FromRgb(43, 43, 43));
            helpWindow.ResizeMode = ResizeMode.NoResize;
            
            // Центрируем окно
            helpWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            
            // Добавляем Page3 в окно
            Page3 helpPage = new Page3();
            helpWindow.Content = helpPage;
            
            helpWindow.Show();
        }

        private void FileButton_Click(object sender, RoutedEventArgs e)
        {
            // Показать контекстное меню при клике на кнопку Файл
            var button = sender as Button;
            if (button?.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция 'Новый файл' пока не реализована", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция 'Открыть' пока не реализована", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция 'Сохранить' пока не реализована", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveAsFile_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция 'Сохранить как' пока не реализована", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // Показать контекстное меню при клике на кнопку Файл
            var button = sender as Button;
            if (button?.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void NewFile_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция 'Новый файл' пока не реализована", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция 'Открыть' пока не реализована", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция 'Сохранить' пока не реализована", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SaveAs_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция 'Сохранить как' пока не реализована", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DecreaseZoom_Click(object sender, MouseButtonEventArgs e)
        {
            if (ZoomSlider != null && ZoomSlider.Value > ZoomSlider.Minimum)
            {
                ZoomSlider.Value = Math.Max(ZoomSlider.Minimum, ZoomSlider.Value - 5);
            }
        }

        private void IncreaseZoom_Click(object sender, MouseButtonEventArgs e)
        {
            if (ZoomSlider != null && ZoomSlider.Value < ZoomSlider.Maximum)
            {
                ZoomSlider.Value = Math.Min(ZoomSlider.Maximum, ZoomSlider.Value + 5);
            }
        }

    }
}
