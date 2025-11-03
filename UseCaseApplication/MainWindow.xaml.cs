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
using System.Windows.Controls.Primitives;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;

namespace UseCaseApplication
{
    public partial class MainWindow : Window
    {
        private readonly Stack<UIElement> otmenaStack = new Stack<UIElement>();
        private readonly Stack<UIElement> vozvratStack = new Stack<UIElement>();
        //private double tekushayaTolschinaLinii = 2.0;
        private const double standartnayaTolschinaLinii = 1.0;
        private double tekushayaTolschinaLinii = 2.0; 

        private Point tochkaNachalaPeretaskivaniya;
        private Button istochnikKnopki;
        private bool peretaskivayuIzPaneli;
        
        private UIElement vybranniyElement;
        private List<UIElement> vybranniyeElementy = new List<UIElement>();
        private Dictionary<UIElement, double> originalnyeTolschiny = new Dictionary<UIElement, double>();
        private Dictionary<UIElement, Rect> originalnyeRazmery = new Dictionary<UIElement, Rect>();
        private Point nachaloPeremesheniya;
        private bool peremeshayuElement;
        private double originalLeft;
        private double originalTop;
        
        private bool peremeshayuHolst;
        private Point nachaloPeremesheniyaHolsta;
        
        // Переменные для масштабирования
        private Border ramkaVydeleniya;
        private List<Border> markeriMashtaba;
        private bool mashtabiruyuElement;
        private Border aktivniyMarker;
        private Point tochkaNachalaMashtabirovaniya;
        private Rect originalniyRazmer;
        private Point originalnayaPozitsiya;
        private UIElement elementDlyaMashtabirovaniya;

        public MainWindow()
        {
            InitializeComponent();
            
            TekstTolschiny.Text = tekushayaTolschinaLinii.ToString();
        }

        private void ZagolovokOkna_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
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

        private void Svernyt_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void Razvernut_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void Zakryt_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void KnopkaInstrumenta_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                istochnikKnopki = button;
                tochkaNachalaPeretaskivaniya = e.GetPosition(button);
                peretaskivayuIzPaneli = false;
            }
        }

        private void KnopkaInstrumenta_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && istochnikKnopki != null && !peretaskivayuIzPaneli)
            {
                var tekushayaPozitsiya = e.GetPosition(istochnikKnopki);
                
                if (Math.Abs(tekushayaPozitsiya.X - tochkaNachalaPeretaskivaniya.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(tekushayaPozitsiya.Y - tochkaNachalaPeretaskivaniya.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    string instrument = istochnikKnopki.Tag as string ?? string.Empty;
                    if (!string.IsNullOrEmpty(instrument))
                    {
                        peretaskivayuIzPaneli = true;
                        DragDrop.DoDragDrop(istochnikKnopki, instrument, DragDropEffects.Copy);
                        peretaskivayuIzPaneli = false;
                    }
                    istochnikKnopki = null;
                }
            }
        }

        private void PolzunokMashtaba_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TransformMashtaba == null || MetkaMashtaba == null) return;
            var mashtab = e.NewValue / 100.0;
            
            var animatsiyaX = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = mashtab,
                Duration = TimeSpan.FromMilliseconds(50),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            
            var animatsiyaY = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = mashtab,
                Duration = TimeSpan.FromMilliseconds(50),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            
            TransformMashtaba.BeginAnimation(ScaleTransform.ScaleXProperty, animatsiyaX);
            TransformMashtaba.BeginAnimation(ScaleTransform.ScaleYProperty, animatsiyaY);
            MetkaMashtaba.Text = $"{(int)e.NewValue}%";
        }

        private void PerekyuchatelSetki_Changed(object sender, RoutedEventArgs e)
        {
            if (FonSetki == null) return;
            
            if (PerekyuchatelSetki.IsChecked == true)
            {
                FonSetki.Visibility = Visibility.Visible;
            }
            else
            {
                FonSetki.Visibility = Visibility.Hidden;
            }
        }

        private void UmenshitTolshinu_Click(object sender, RoutedEventArgs e)
        {
            if (tekushayaTolschinaLinii > 1)
            {
                tekushayaTolschinaLinii--;
                TekstTolschiny.Text = tekushayaTolschinaLinii.ToString();
                ObnovitTolshinuLinii();
            }
        }

        private void UvelichitTolshinu_Click(object sender, RoutedEventArgs e)
        {
            if (tekushayaTolschinaLinii < 10)
            {
                tekushayaTolschinaLinii++;
                TekstTolschiny.Text = tekushayaTolschinaLinii.ToString();
                ObnovitTolshinuLinii();
            }
        }

        private void ObnovitTolshinuLinii()
        {

            if (vybranniyeElementy == null || vybranniyeElementy.Count == 0) return;

            foreach (var element in vybranniyeElementy.ToList())
            {
                if (element is Shape forma)
                {
                    forma.StrokeThickness = tekushayaTolschinaLinii;
                    originalnyeTolschiny[element] = tekushayaTolschinaLinii;
                }
                else if (element is Canvas canvas)
                {
                    foreach (var docherniy in canvas.Children.OfType<Shape>())
                    {
                        docherniy.StrokeThickness = tekushayaTolschinaLinii;
                        var key = docherniy as UIElement;
                        if (key != null)
                        {
                            originalnyeTolschiny[key] = tekushayaTolschinaLinii;
                        }
                    }
                }
            }
        }

        private void PoleDlyaRisovaniya_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (peretaskivayuIzPaneli)
            {
                return;
            }
            
            // Проверяем, не кликнули ли мы на маркер масштабирования
            if (e.OriginalSource is Border marker && markeriMashtaba != null && markeriMashtaba.Contains(marker))
            {
                return; // Маркер обработает событие сам
            }
            
            var element = e.OriginalSource as UIElement;
            
            if (element == PoleDlyaRisovaniya || element == FonSetki || element == ramkaVydeleniya)
            {
                SnytVydelenie();
                peremeshayuHolst = true;
                nachaloPeremesheniyaHolsta = e.GetPosition(this);
                Mouse.Capture(PoleDlyaRisovaniya);
                PoleDlyaRisovaniya.Cursor = Cursors.Hand;
                return;
            }
            
            var roditelskiyElement = NaytiElementNaHolste(element);
            
            if (roditelskiyElement != null && PoleDlyaRisovaniya.Children.Contains(roditelskiyElement))
            {
                bool shiftNazhat = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
                
                if (!shiftNazhat)
                {
                    SnytVydelenie();
                }
                
                vybranniyElement = roditelskiyElement;
                peremeshayuElement = true;
                nachaloPeremesheniya = e.GetPosition(PoleDlyaRisovaniya);
                
                var tekushiyLeft = Canvas.GetLeft(vybranniyElement);
                var tekushiyTop = Canvas.GetTop(vybranniyElement);
                originalLeft = double.IsNaN(tekushiyLeft) ? 0 : tekushiyLeft;
                originalTop = double.IsNaN(tekushiyTop) ? 0 : tekushiyTop;
                
                Mouse.Capture(PoleDlyaRisovaniya);
                
                if (!vybranniyeElementy.Contains(vybranniyElement))
                {
                    vybranniyeElementy.Add(vybranniyElement);
                }
                
                VydelitElement(vybranniyElement);
                ObnovitSchetchikTolschiny(vybranniyElement);


            }
        }
        
        private void VydelitElement(UIElement element)
        {
            if (element is Shape forma)
            {
                
                if (!originalnyeTolschiny.ContainsKey(element))
                {
                    originalnyeTolschiny[element] = forma.StrokeThickness;
                }
                forma.Stroke = Brushes.DodgerBlue;
                forma.StrokeThickness = 2;
            }
            else if (element is Canvas canvas)
            {
                foreach (var docherniy in canvas.Children.OfType<Shape>())
                {
                    var key = docherniy as UIElement;
                    if (key != null && !originalnyeTolschiny.ContainsKey(key))
                    {
                        originalnyeTolschiny[key] = docherniy.StrokeThickness;
                    }
                    docherniy.Stroke = Brushes.DodgerBlue;
                    docherniy.StrokeThickness = 2;
                }
            }
            
            PokazatRamuMashtabirovaniya(element);
        }
        
        private void SkrytRamuMashtabirovaniya()
        {
            if (ramkaVydeleniya != null)
            {
                PoleDlyaRisovaniya.Children.Remove(ramkaVydeleniya);
                ramkaVydeleniya = null;
            }
            
            if (markeriMashtaba != null)
            {
                foreach (var marker in markeriMashtaba)
                {
                    if (PoleDlyaRisovaniya.Children.Contains(marker))
                    {
                        PoleDlyaRisovaniya.Children.Remove(marker);
                    }
                }
                markeriMashtaba.Clear();
            }
        }
        
        private void PokazatRamuMashtabirovaniya(UIElement element)
        {
            if (element == null || !PoleDlyaRisovaniya.Children.Contains(element)) 
            {
                SkrytRamuMashtabirovaniya();
                return;
            }
            
            SkrytRamuMashtabirovaniya();
            
            var bounds = PoluchitGranitsyElementa(element);
            if (bounds.Width <= 0 || bounds.Height <= 0) 
            {
                // Если границы не определены, используем значения по умолчанию
                bounds = new Rect(bounds.Left, bounds.Top, 120, 150);
            }
            
            // Создаем рамку выделения (пунктирная граница)
            var rect = new Rectangle
            {
                Stroke = new SolidColorBrush(Color.FromRgb(30, 144, 255)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection(new[] { 4.0, 3.0 }),
                Fill = Brushes.Transparent,
                Width = bounds.Width + 8,
                Height = bounds.Height + 8,
                RadiusX = 2,
                RadiusY = 2,
                IsHitTestVisible = false
            };
            
            ramkaVydeleniya = new Border
            {
                Child = rect,
                Background = Brushes.Transparent,
                IsHitTestVisible = false
            };
            
            Canvas.SetLeft(ramkaVydeleniya, bounds.Left - 4);
            Canvas.SetTop(ramkaVydeleniya, bounds.Top - 4);
            Panel.SetZIndex(ramkaVydeleniya, 1000);
            PoleDlyaRisovaniya.Children.Add(ramkaVydeleniya);
            
            // Обновляем список маркеров перед созданием новых
            if (markeriMashtaba == null)
            {
                markeriMashtaba = new List<Border>();
            }
            
            // Создаем маркеры изменения размера (8 штук: 4 угла + 4 стороны)
            markeriMashtaba = new List<Border>();
            double markerSize = 8;
            
            // Угловые маркеры
            var positions = new[]
            {
                new Point(-4, -4),           // Левый верхний
                new Point(bounds.Width + 4, -4), // Правый верхний
                new Point(-4, bounds.Height + 4), // Левый нижний
                new Point(bounds.Width + 4, bounds.Height + 4), // Правый нижний
                new Point(bounds.Width / 2, -4), // Верхний центр
                new Point(bounds.Width + 4, bounds.Height / 2), // Правый центр
                new Point(bounds.Width / 2, bounds.Height + 4), // Нижний центр
                new Point(-4, bounds.Height / 2) // Левый центр
            };
            
            Cursor[] cursors = { Cursors.SizeNWSE, Cursors.SizeNESW, Cursors.SizeNESW, Cursors.SizeNWSE, 
                                 Cursors.SizeNS, Cursors.SizeWE, Cursors.SizeNS, Cursors.SizeWE };
            
            for (int i = 0; i < positions.Length; i++)
            {
                var marker = new Border
                {
                    Width = markerSize,
                    Height = markerSize,
                    Background = Brushes.White,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(30, 144, 255)),
                    BorderThickness = new Thickness(1.5),
                    Cursor = cursors[i],
                    SnapsToDevicePixels = true,
                    Opacity = 1.0,
                    IsHitTestVisible = true,
                    CornerRadius = new CornerRadius(1),
                    ClipToBounds = false
                };
                
                // Добавляем эффект тени для лучшей видимости
                marker.Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 3,
                    Opacity = 0.5,
                    ShadowDepth = 1
                };
                
                Canvas.SetLeft(marker, bounds.Left + positions[i].X - markerSize / 2);
                Canvas.SetTop(marker, bounds.Top + positions[i].Y - markerSize / 2);
                Panel.SetZIndex(marker, 1001);
                
                marker.MouseLeftButtonDown += Marker_MouseLeftButtonDown;
                marker.MouseMove += Marker_MouseMove;
                marker.MouseLeftButtonUp += Marker_MouseLeftButtonUp;
                
                // Сохраняем индекс маркера для быстрого доступа
                marker.Tag = i;
                
                PoleDlyaRisovaniya.Children.Add(marker);
                markeriMashtaba.Add(marker);
            }
        }
        
        private Rect PoluchitGranitsyElementa(UIElement element)
        {
            if (element == null) return new Rect();
            
            var left = Canvas.GetLeft(element);
            var top = Canvas.GetTop(element);
            
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            
            double width = 0, height = 0;
            double scaleX = 1.0, scaleY = 1.0;
            
            // Проверяем масштабирование через RenderTransform
            if (element.RenderTransform is ScaleTransform scaleTransform)
            {
                scaleX = scaleTransform.ScaleX;
                scaleY = scaleTransform.ScaleY;
            }
            
            if (element is Shape shape)
            {
                // Для Line вычисляем размер на основе координат
                if (shape is Line line)
                {
                    width = Math.Abs(line.X2 - line.X1) * scaleX;
                    height = Math.Abs(line.Y2 - line.Y1) * scaleY;
                    if (width < Math.Abs(line.X2 - line.X1)) width = Math.Abs(line.X2 - line.X1);
                    if (height < Math.Abs(line.Y2 - line.Y1)) height = Math.Abs(line.Y2 - line.Y1);
                }
                else
                {
                    shape.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    width = shape.DesiredSize.Width * scaleX;
                    height = shape.DesiredSize.Height * scaleY;
                    
                    // Если у Shape есть явные размеры, используем их
                    if (!double.IsNaN(shape.Width) && shape.Width > 0)
                        width = shape.Width * scaleX;
                    if (!double.IsNaN(shape.Height) && shape.Height > 0)
                        height = shape.Height * scaleY;
                }
            }
            else if (element is Canvas canvas)
            {
                // Для Canvas вычисляем размер на основе всех дочерних элементов
                double minLeft = double.MaxValue, minTop = double.MaxValue;
                double maxRight = 0, maxBottom = 0;
                bool hasChildren = false;
                
                foreach (UIElement child in canvas.Children)
                {
                    double childLeft = 0, childTop = 0;
                    double childRight = 0, childBottom = 0;
                    
                    if (child is Shape childShape)
                    {
                        if (childShape is Line line)
                        {
                            // Для Line координаты X1, Y1, X2, Y2 абсолютные внутри Canvas
                            childLeft = Math.Min(line.X1, line.X2);
                            childTop = Math.Min(line.Y1, line.Y2);
                            childRight = Math.Max(line.X1, line.X2);
                            childBottom = Math.Max(line.Y1, line.Y2);
                            // Учитываем толщину линии
                            double strokeThickness = line.StrokeThickness > 0 ? line.StrokeThickness : 1;
                            childLeft -= strokeThickness / 2;
                            childTop -= strokeThickness / 2;
                            childRight += strokeThickness / 2;
                            childBottom += strokeThickness / 2;
                        }
                        else
                        {
                            // Для других Shape учитываем Canvas.GetLeft/Top
                            childLeft = Canvas.GetLeft(child);
                            childTop = Canvas.GetTop(child);
                            if (double.IsNaN(childLeft)) childLeft = 0;
                            if (double.IsNaN(childTop)) childTop = 0;
                            
                            childShape.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                            double childWidth = !double.IsNaN(childShape.Width) && childShape.Width > 0 
                                ? childShape.Width 
                                : childShape.DesiredSize.Width;
                            double childHeight = !double.IsNaN(childShape.Height) && childShape.Height > 0 
                                ? childShape.Height 
                                : childShape.DesiredSize.Height;
                            
                            childRight = childLeft + childWidth;
                            childBottom = childTop + childHeight;
                        }
                    }
                    else if (child is FrameworkElement childFe)
                    {
                        childLeft = Canvas.GetLeft(child);
                        childTop = Canvas.GetTop(child);
                        if (double.IsNaN(childLeft)) childLeft = 0;
                        if (double.IsNaN(childTop)) childTop = 0;
                        
                        childFe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        double childWidth = !double.IsNaN(childFe.Width) && childFe.Width > 0 
                            ? childFe.Width 
                            : childFe.DesiredSize.Width;
                        double childHeight = !double.IsNaN(childFe.Height) && childFe.Height > 0 
                            ? childFe.Height 
                            : childFe.DesiredSize.Height;
                        
                        childRight = childLeft + childWidth;
                        childBottom = childTop + childHeight;
                    }
                    
                    if (childRight > childLeft || childBottom > childTop)
                    {
                        hasChildren = true;
                        minLeft = Math.Min(minLeft, childLeft);
                        minTop = Math.Min(minTop, childTop);
                        maxRight = Math.Max(maxRight, childRight);
                        maxBottom = Math.Max(maxBottom, childBottom);
                    }
                }
                
                if (hasChildren)
                {
                    width = (maxRight - minLeft) * scaleX;
                    height = (maxBottom - minTop) * scaleY;
                    // Корректируем позицию рамки с учетом минимальной позиции дочерних элементов
                    // При масштабировании Canvas с RenderTransformOrigin=(0,0) содержимое масштабируется от левого верхнего угла
                    // Поэтому позиция начала содержимого = позиция Canvas + minLeft * scaleX
                    left = left + minLeft * scaleX;
                    top = top + minTop * scaleY;
                }
                else
                {
                    width = 120 * scaleX;
                    height = 150 * scaleY;
                }
                
                // Если у Canvas есть явные размеры, используем их
                if (!double.IsNaN(canvas.Width) && canvas.Width > 0)
                    width = canvas.Width * scaleX;
                if (!double.IsNaN(canvas.Height) && canvas.Height > 0)
                    height = canvas.Height * scaleY;
            }
            else if (element is TextBlock textBlock)
            {
                textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                width = textBlock.DesiredSize.Width;
                height = textBlock.DesiredSize.Height;
            }
            else if (element is FrameworkElement fe)
            {
                fe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                width = fe.DesiredSize.Width * scaleX;
                height = fe.DesiredSize.Height * scaleY;
                
                if (!double.IsNaN(fe.Width) && fe.Width > 0)
                    width = fe.Width * scaleX;
                if (!double.IsNaN(fe.Height) && fe.Height > 0)
                    height = fe.Height * scaleY;
            }
            
            return new Rect(left, top, width, height);
        }
        
        private Rect PoluchitGranitsyBezMashtaba(UIElement element)
        {
            if (element == null) return new Rect();
            
            var left = Canvas.GetLeft(element);
            var top = Canvas.GetTop(element);
            
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;
            
            double width = 0, height = 0;
            
            if (element is Shape shape)
            {
                // Для Line вычисляем размер на основе координат
                if (shape is Line line)
                {
                    // Получаем оригинальные координаты, убирая трансформацию
                    double x1 = line.X1, y1 = line.Y1, x2 = line.X2, y2 = line.Y2;
                    if (line.RenderTransform is ScaleTransform scaleTransform)
                    {
                        x1 = x1 / scaleTransform.ScaleX;
                        y1 = y1 / scaleTransform.ScaleY;
                        x2 = x2 / scaleTransform.ScaleX;
                        y2 = y2 / scaleTransform.ScaleY;
                    }
                    width = Math.Abs(x2 - x1);
                    height = Math.Abs(y2 - y1);
                    if (width == 0) width = 120;
                    if (height == 0) height = 60;
                }
                else
                {
                    shape.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    width = !double.IsNaN(shape.Width) && shape.Width > 0 
                        ? shape.Width 
                        : shape.DesiredSize.Width;
                    height = !double.IsNaN(shape.Height) && shape.Height > 0 
                        ? shape.Height 
                        : shape.DesiredSize.Height;
                    
                    // Убираем эффект трансформации
                    if (shape.RenderTransform is ScaleTransform scaleTransform)
                    {
                        width = width / scaleTransform.ScaleX;
                        height = height / scaleTransform.ScaleY;
                    }
                }
            }
            else if (element is Canvas canvas)
            {
                // Для Canvas вычисляем размер на основе всех дочерних элементов
                double minLeft = double.MaxValue, minTop = double.MaxValue;
                double maxRight = 0, maxBottom = 0;
                bool hasChildren = false;
                
                foreach (UIElement child in canvas.Children)
                {
                    double childLeft = 0, childTop = 0;
                    double childRight = 0, childBottom = 0;
                    
                    if (child is Shape childShape)
                    {
                        if (childShape is Line line)
                        {
                            // Для Line координаты X1, Y1, X2, Y2 абсолютные внутри Canvas
                            childLeft = Math.Min(line.X1, line.X2);
                            childTop = Math.Min(line.Y1, line.Y2);
                            childRight = Math.Max(line.X1, line.X2);
                            childBottom = Math.Max(line.Y1, line.Y2);
                            // Учитываем толщину линии
                            double strokeThickness = line.StrokeThickness > 0 ? line.StrokeThickness : 1;
                            childLeft -= strokeThickness / 2;
                            childTop -= strokeThickness / 2;
                            childRight += strokeThickness / 2;
                            childBottom += strokeThickness / 2;
                        }
                        else
                        {
                            // Для других Shape учитываем Canvas.GetLeft/Top
                            childLeft = Canvas.GetLeft(child);
                            childTop = Canvas.GetTop(child);
                            if (double.IsNaN(childLeft)) childLeft = 0;
                            if (double.IsNaN(childTop)) childTop = 0;
                            
                            childShape.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                            double childWidth = !double.IsNaN(childShape.Width) && childShape.Width > 0 
                                ? childShape.Width 
                                : childShape.DesiredSize.Width;
                            double childHeight = !double.IsNaN(childShape.Height) && childShape.Height > 0 
                                ? childShape.Height 
                                : childShape.DesiredSize.Height;
                            
                            childRight = childLeft + childWidth;
                            childBottom = childTop + childHeight;
                        }
                    }
                    else if (child is FrameworkElement childFe)
                    {
                        childLeft = Canvas.GetLeft(child);
                        childTop = Canvas.GetTop(child);
                        if (double.IsNaN(childLeft)) childLeft = 0;
                        if (double.IsNaN(childTop)) childTop = 0;
                        
                        childFe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        double childWidth = !double.IsNaN(childFe.Width) && childFe.Width > 0 
                            ? childFe.Width 
                            : childFe.DesiredSize.Width;
                        double childHeight = !double.IsNaN(childFe.Height) && childFe.Height > 0 
                            ? childFe.Height 
                            : childFe.DesiredSize.Height;
                        
                        childRight = childLeft + childWidth;
                        childBottom = childTop + childHeight;
                    }
                    
                    if (childRight > childLeft || childBottom > childTop)
                    {
                        hasChildren = true;
                        minLeft = Math.Min(minLeft, childLeft);
                        minTop = Math.Min(minTop, childTop);
                        maxRight = Math.Max(maxRight, childRight);
                        maxBottom = Math.Max(maxBottom, childBottom);
                    }
                }
                
                if (hasChildren)
                {
                    width = maxRight - minLeft;
                    height = maxBottom - minTop;
                }
                else
                {
                    width = 120;
                    height = 150;
                }
                
                // Убираем эффект трансформации Canvas
                if (canvas.RenderTransform is ScaleTransform scaleTransform)
                {
                    width = width / scaleTransform.ScaleX;
                    height = height / scaleTransform.ScaleY;
                }
            }
            else if (element is TextBlock textBlock)
            {
                textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                width = textBlock.DesiredSize.Width;
                height = textBlock.DesiredSize.Height;
            }
            else if (element is FrameworkElement fe)
            {
                fe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                width = !double.IsNaN(fe.Width) && fe.Width > 0 
                    ? fe.Width 
                    : fe.DesiredSize.Width;
                height = !double.IsNaN(fe.Height) && fe.Height > 0 
                    ? fe.Height 
                    : fe.DesiredSize.Height;
                
                if (fe.RenderTransform is ScaleTransform scaleTransform)
                {
                    width = width / scaleTransform.ScaleX;
                    height = height / scaleTransform.ScaleY;
                }
            }
            
            return new Rect(left, top, width, height);
        }
        
        private void ObnovitSchetchikTolschiny(UIElement element)
        {
            double tolstina = standartnayaTolschinaLinii;

            if (element is Shape forma)
            {
                if (originalnyeTolschiny.ContainsKey(element))
                {
                    tolstina = originalnyeTolschiny[element];
                }
                else
                {
                    tolstina = standartnayaTolschinaLinii;
                }
            }
            else if (element is Canvas canvas)
            {
                var perviyDocherniy = canvas.Children.OfType<Shape>().FirstOrDefault();
                if (perviyDocherniy != null)
                {
                    var key = perviyDocherniy as UIElement;
                    if (key != null && originalnyeTolschiny.ContainsKey(key))
                    {
                        tolstina = originalnyeTolschiny[key];
                    }
                    else
                    {
                        tolstina = standartnayaTolschinaLinii;
                    }
                }
            }

            tekushayaTolschinaLinii = tolstina;
            if (TekstTolschiny != null)
            {
                TekstTolschiny.Text = tekushayaTolschinaLinii.ToString();
            }
        }

        private void SnytVydelenie()
        {
            foreach (var element in vybranniyeElementy.ToList())
            {
                if (element is Shape forma)
                {
                    forma.Stroke = Brushes.Black;
                    if (originalnyeTolschiny.ContainsKey(element))
                    {
                        forma.StrokeThickness = originalnyeTolschiny[element];
                        originalnyeTolschiny.Remove(element);
                    }
                    else
                    {
                        forma.StrokeThickness = standartnayaTolschinaLinii;
                    }
                }
                else if (element is Canvas canvas)
                {
                    foreach (var docherniy in canvas.Children.OfType<Shape>())
                    {
                        docherniy.Stroke = Brushes.Black;
                        var key = docherniy as UIElement;
                        if (key != null && originalnyeTolschiny.ContainsKey(key))
                        {
                            docherniy.StrokeThickness = originalnyeTolschiny[key];
                            originalnyeTolschiny.Remove(key);
                        }
                        else
                        {
                            docherniy.StrokeThickness = standartnayaTolschinaLinii;
                        }
                    }
                }
            }
            vybranniyeElementy.Clear();
            originalnyeTolschiny.Clear();
            SkrytRamuMashtabirovaniya();
        }
        
        private void Marker_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (vybranniyElement == null) return;
            
            aktivniyMarker = sender as Border;
            if (aktivniyMarker == null) return;
            
            mashtabiruyuElement = true;
            elementDlyaMashtabirovaniya = vybranniyElement;
            tochkaNachalaMashtabirovaniya = e.GetPosition(PoleDlyaRisovaniya);
            
            // Получаем реальные размеры без учета текущего масштабирования
            if (!originalnyeRazmery.ContainsKey(elementDlyaMashtabirovaniya))
            {
                var realBounds = PoluchitGranitsyBezMashtaba(elementDlyaMashtabirovaniya);
                originalnyeRazmery[elementDlyaMashtabirovaniya] = realBounds;
                originalniyRazmer = realBounds;
            }
            else
            {
                var savedBounds = originalnyeRazmery[elementDlyaMashtabirovaniya];
                var currentPos = PoluchitGranitsyElementa(elementDlyaMashtabirovaniya);
                originalniyRazmer = new Rect(currentPos.Left, currentPos.Top, savedBounds.Width, savedBounds.Height);
            }
            
            originalnayaPozitsiya = new Point(originalniyRazmer.Left, originalniyRazmer.Top);
            
            // Захватываем мышь на Canvas, чтобы события продолжали обрабатываться даже если курсор покинет маркер
            Mouse.Capture(PoleDlyaRisovaniya);
            e.Handled = true;
        }
        
        private void Marker_MouseMove(object sender, MouseEventArgs e)
        {
            if (!mashtabiruyuElement || aktivniyMarker == null || elementDlyaMashtabirovaniya == null)
                return;
                
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                mashtabiruyuElement = false;
                aktivniyMarker.ReleaseMouseCapture();
                Mouse.Capture(null);
                return;
            }
            
            var tekushayaPoz = e.GetPosition(PoleDlyaRisovaniya);
            var deltaX = tekushayaPoz.X - tochkaNachalaMashtabirovaniya.X;
            var deltaY = tekushayaPoz.Y - tochkaNachalaMashtabirovaniya.Y;
            
            // Получаем индекс маркера из Tag
            int markerIndex = -1;
            if (aktivniyMarker.Tag is int index)
            {
                markerIndex = index;
            }
            else if (markeriMashtaba != null)
            {
                markerIndex = markeriMashtaba.IndexOf(aktivniyMarker);
            }
            
            if (markerIndex < 0 || markerIndex >= 8) return;
            
            // Определяем, какой маркер используется и как масштабировать
            double newWidth = originalniyRazmer.Width;
            double newHeight = originalniyRazmer.Height;
            double newLeft = originalnayaPozitsiya.X;
            double newTop = originalnayaPozitsiya.Y;
            
            switch (markerIndex)
            {
                case 0: // Левый верхний
                    newWidth = Math.Max(20, originalniyRazmer.Width - deltaX);
                    newHeight = Math.Max(20, originalniyRazmer.Height - deltaY);
                    newLeft = originalnayaPozitsiya.X + (originalniyRazmer.Width - newWidth);
                    newTop = originalnayaPozitsiya.Y + (originalniyRazmer.Height - newHeight);
                    break;
                case 1: // Правый верхний
                    newWidth = Math.Max(20, originalniyRazmer.Width + deltaX);
                    newHeight = Math.Max(20, originalniyRazmer.Height - deltaY);
                    newTop = originalnayaPozitsiya.Y + (originalniyRazmer.Height - newHeight);
                    break;
                case 2: // Левый нижний
                    newWidth = Math.Max(20, originalniyRazmer.Width - deltaX);
                    newHeight = Math.Max(20, originalniyRazmer.Height + deltaY);
                    newLeft = originalnayaPozitsiya.X + (originalniyRazmer.Width - newWidth);
                    break;
                case 3: // Правый нижний
                    newWidth = Math.Max(20, originalniyRazmer.Width + deltaX);
                    newHeight = Math.Max(20, originalniyRazmer.Height + deltaY);
                    break;
                case 4: // Верхний центр
                    newHeight = Math.Max(20, originalniyRazmer.Height - deltaY);
                    newTop = originalnayaPozitsiya.Y + (originalniyRazmer.Height - newHeight);
                    break;
                case 5: // Правый центр
                    newWidth = Math.Max(20, originalniyRazmer.Width + deltaX);
                    break;
                case 6: // Нижний центр
                    newHeight = Math.Max(20, originalniyRazmer.Height + deltaY);
                    break;
                case 7: // Левый центр
                    newWidth = Math.Max(20, originalniyRazmer.Width - deltaX);
                    newLeft = originalnayaPozitsiya.X + (originalniyRazmer.Width - newWidth);
                    break;
            }
            
            // Убеждаемся, что размеры больше 0
            if (newWidth > 0 && newHeight > 0 && originalniyRazmer.Width > 0 && originalniyRazmer.Height > 0)
            {
                MashtabirovatElement(elementDlyaMashtabirovaniya, newLeft, newTop, newWidth, newHeight);
                PokazatRamuMashtabirovaniya(elementDlyaMashtabirovaniya);
            }
        }
        
        private void Marker_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            mashtabiruyuElement = false;
            if (aktivniyMarker != null)
            {
                aktivniyMarker.ReleaseMouseCapture();
            }
            aktivniyMarker = null;
            Mouse.Capture(null);
            
            if (elementDlyaMashtabirovaniya != null)
            {
                PokazatRamuMashtabirovaniya(elementDlyaMashtabirovaniya);
            }
        }
        
        private void MashtabirovatElement(UIElement element, double left, double top, double width, double height)
        {
            if (element == null || width <= 0 || height <= 0) return;
            
            // Получаем оригинальные размеры без масштабирования
            Rect baseBounds;
            if (originalnyeRazmery.ContainsKey(element))
            {
                baseBounds = originalnyeRazmery[element];
            }
            else
            {
                // Если размеры не сохранены, сохраняем их сейчас
                baseBounds = PoluchitGranitsyBezMashtaba(element);
                originalnyeRazmery[element] = baseBounds;
            }
            
            if (baseBounds.Width <= 0 || baseBounds.Height <= 0) return;
            
            var scaleX = width / baseBounds.Width;
            var scaleY = height / baseBounds.Height;
            
            // Для Canvas масштабируем через RenderTransform и масштабируем дочерние элементы
            if (element is Canvas canvas)
            {
                // Сохраняем оригинальные размеры и позиции дочерних элементов при первом масштабировании
                if (!originalnyeRazmery.ContainsKey(element))
                {
                    // Вычисляем реальные размеры Canvas на основе содержимого
                    double maxRight = 0, maxBottom = 0;
                    foreach (UIElement child in canvas.Children)
                    {
                        var childLeft = Canvas.GetLeft(child);
                        var childTop = Canvas.GetTop(child);
                        if (double.IsNaN(childLeft)) childLeft = 0;
                        if (double.IsNaN(childTop)) childTop = 0;
                        
                        double childWidth = 0, childHeight = 0;
                        if (child is Shape childShape)
                        {
                            childShape.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                            childWidth = !double.IsNaN(childShape.Width) && childShape.Width > 0 
                                ? childShape.Width 
                                : childShape.DesiredSize.Width;
                            childHeight = !double.IsNaN(childShape.Height) && childShape.Height > 0 
                                ? childShape.Height 
                                : childShape.DesiredSize.Height;
                        }
                        else if (child is FrameworkElement childFe)
                        {
                            childFe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                            childWidth = !double.IsNaN(childFe.Width) && childFe.Width > 0 
                                ? childFe.Width 
                                : childFe.DesiredSize.Width;
                            childHeight = !double.IsNaN(childFe.Height) && childFe.Height > 0 
                                ? childFe.Height 
                                : childFe.DesiredSize.Height;
                        }
                        
                        maxRight = Math.Max(maxRight, childLeft + childWidth);
                        maxBottom = Math.Max(maxBottom, childTop + childHeight);
                    }
                    
                    var canvasLeft = Canvas.GetLeft(canvas);
                    var canvasTop = Canvas.GetTop(canvas);
                    if (double.IsNaN(canvasLeft)) canvasLeft = 0;
                    if (double.IsNaN(canvasTop)) canvasTop = 0;
                    
                    originalnyeRazmery[element] = new Rect(
                        canvasLeft,
                        canvasTop,
                        maxRight > 0 ? maxRight : 120,
                        maxBottom > 0 ? maxBottom : 150
                    );
                    
                    // Сохраняем оригинальные позиции и размеры дочерних элементов
                    foreach (UIElement child in canvas.Children)
                    {
                        var childFe = child as FrameworkElement;
                        if (childFe != null && childFe.Tag == null)
                        {
                            var childInfo = new Dictionary<string, double>();
                            childInfo["Left"] = Canvas.GetLeft(child);
                            childInfo["Top"] = Canvas.GetTop(child);
                            if (child is Shape childShape)
                            {
                                childShape.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                                childInfo["Width"] = !double.IsNaN(childShape.Width) && childShape.Width > 0 
                                    ? childShape.Width 
                                    : childShape.DesiredSize.Width;
                                childInfo["Height"] = !double.IsNaN(childShape.Height) && childShape.Height > 0 
                                    ? childShape.Height 
                                    : childShape.DesiredSize.Height;
                            }
                            else if (child is FrameworkElement childFrameworkElement)
                            {
                                childFrameworkElement.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                                childInfo["Width"] = !double.IsNaN(childFrameworkElement.Width) && childFrameworkElement.Width > 0 
                                    ? childFrameworkElement.Width 
                                    : childFrameworkElement.DesiredSize.Width;
                                childInfo["Height"] = !double.IsNaN(childFrameworkElement.Height) && childFrameworkElement.Height > 0 
                                    ? childFrameworkElement.Height 
                                    : childFrameworkElement.DesiredSize.Height;
                            }
                            childFe.Tag = childInfo;
                        }
                    }
                }
                
                // Масштабируем Canvas через RenderTransform
                var transform = canvas.RenderTransform as ScaleTransform;
                if (transform == null)
                {
                    transform = new ScaleTransform(1.0, 1.0);
                    canvas.RenderTransform = transform;
                    canvas.RenderTransformOrigin = new Point(0, 0);
                }
                
                transform.ScaleX = scaleX;
                transform.ScaleY = scaleY;
                
                // Устанавливаем новую позицию
                Canvas.SetLeft(canvas, left);
                Canvas.SetTop(canvas, top);
                
                // Принудительно обновляем визуализацию
                canvas.InvalidateVisual();
                canvas.UpdateLayout();
            }
            // Для Shape изменяем размеры напрямую или через трансформацию
            else if (element is Shape shape)
            {
                // Для Line масштабируем координаты напрямую
                if (shape is Line lineForScale)
                {
                    // Сохраняем оригинальные координаты при первом масштабировании
                    if (!originalnyeRazmery.ContainsKey(element))
                    {
                        double origX1 = lineForScale.X1;
                        double origY1 = lineForScale.Y1;
                        double origX2 = lineForScale.X2;
                        double origY2 = lineForScale.Y2;
                        // Сохраняем координаты в словаре для Line (используем словарь с другим ключом)
                        var lineCoords = new Dictionary<string, double>();
                        lineCoords["X1"] = origX1;
                        lineCoords["Y1"] = origY1;
                        lineCoords["X2"] = origX2;
                        lineCoords["Y2"] = origY2;
                        // Используем Tag для хранения оригинальных координат (Line наследуется от FrameworkElement, так что Tag доступен)
                        lineForScale.Tag = lineCoords;
                    }
                    
                    var origCoords = lineForScale.Tag as Dictionary<string, double>;
                    if (origCoords != null)
                    {
                        lineForScale.X1 = origCoords["X1"] * scaleX;
                        lineForScale.Y1 = origCoords["Y1"] * scaleY;
                        lineForScale.X2 = origCoords["X2"] * scaleX;
                        lineForScale.Y2 = origCoords["Y2"] * scaleY;
                    }
                }
                else if (shape is Path path)
                {
                    var transform = path.RenderTransform as ScaleTransform;
                    if (transform == null)
                    {
                        transform = new ScaleTransform(1.0, 1.0, 0, 0);
                        path.RenderTransform = transform;
                        path.RenderTransformOrigin = new Point(0, 0);
                    }
                    transform.ScaleX = scaleX;
                    transform.ScaleY = scaleY;
                }
                else
                {
                    // Для Ellipse, Rectangle и других можно менять размеры напрямую
                    shape.Width = baseBounds.Width * scaleX;
                    shape.Height = baseBounds.Height * scaleY;
                }
                
                // Обновляем визуализацию для всех Shape
                shape.InvalidateVisual();
                
                Canvas.SetLeft(shape, left);
                Canvas.SetTop(shape, top);
            }
            // Для TextBlock изменяем FontSize пропорционально
            else if (element is TextBlock textBlock)
            {
                // Сохраняем оригинальный размер шрифта
                if (!originalnyeRazmery.ContainsKey(element))
                {
                    textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    originalnyeRazmery[element] = new Rect(left, top, textBlock.DesiredSize.Width, textBlock.DesiredSize.Height);
                }
                
                var fontSizeScale = Math.Min(scaleX, scaleY);
                if (!originalnyeTolschiny.ContainsKey(element))
                {
                    originalnyeTolschiny[element] = textBlock.FontSize > 0 ? textBlock.FontSize : 16;
                }
                var baseFontSize = originalnyeTolschiny[element];
                textBlock.FontSize = baseFontSize * fontSizeScale;
                Canvas.SetLeft(textBlock, left);
                Canvas.SetTop(textBlock, top);
            }
            else if (element is FrameworkElement fe)
            {
                if (!originalnyeRazmery.ContainsKey(element))
                {
                    fe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    originalnyeRazmery[element] = new Rect(left, top, fe.DesiredSize.Width, fe.DesiredSize.Height);
                }
                
                var baseRect = originalnyeRazmery[element];
                fe.Width = baseRect.Width * scaleX;
                fe.Height = baseRect.Height * scaleY;
                Canvas.SetLeft(fe, left);
                Canvas.SetTop(fe, top);
            }
        }

        private void PoleDlyaRisovaniya_DragOver(object sender, DragEventArgs e)
        {
            if (peremeshayuElement || peremeshayuHolst)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }
            
            if (peretaskivayuIzPaneli && e.Data.GetDataPresent(DataFormats.StringFormat))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }

        private void PoleDlyaRisovaniya_MouseMove(object sender, MouseEventArgs e)
        {
            // Если масштабируем, обрабатываем масштабирование
            if (mashtabiruyuElement && aktivniyMarker != null && elementDlyaMashtabirovaniya != null)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    var tekushayaPoz = e.GetPosition(PoleDlyaRisovaniya);
                    var deltaX = tekushayaPoz.X - tochkaNachalaMashtabirovaniya.X;
                    var deltaY = tekushayaPoz.Y - tochkaNachalaMashtabirovaniya.Y;
                    
                    // Получаем индекс маркера из Tag
                    int markerIndex = -1;
                    if (aktivniyMarker.Tag is int index)
                    {
                        markerIndex = index;
                    }
                    else if (markeriMashtaba != null)
                    {
                        markerIndex = markeriMashtaba.IndexOf(aktivniyMarker);
                    }
                    
                    if (markerIndex >= 0 && markerIndex < 8)
                    {
                        double newWidth = originalniyRazmer.Width;
                        double newHeight = originalniyRazmer.Height;
                        double newLeft = originalnayaPozitsiya.X;
                        double newTop = originalnayaPozitsiya.Y;
                        
                        switch (markerIndex)
                        {
                            case 0: // Левый верхний
                                newWidth = originalniyRazmer.Width - deltaX;
                                newHeight = originalniyRazmer.Height - deltaY;
                                newLeft = originalnayaPozitsiya.X + deltaX;
                                newTop = originalnayaPozitsiya.Y + deltaY;
                                break;
                            case 1: // Правый верхний
                                newWidth = originalniyRazmer.Width + deltaX;
                                newHeight = originalniyRazmer.Height - deltaY;
                                newTop = originalnayaPozitsiya.Y + deltaY;
                                break;
                            case 2: // Левый нижний
                                newWidth = originalniyRazmer.Width - deltaX;
                                newHeight = originalniyRazmer.Height + deltaY;
                                newLeft = originalnayaPozitsiya.X + deltaX;
                                break;
                            case 3: // Правый нижний
                                newWidth = originalniyRazmer.Width + deltaX;
                                newHeight = originalniyRazmer.Height + deltaY;
                                break;
                            case 4: // Верхний центр
                                newHeight = originalniyRazmer.Height - deltaY;
                                newTop = originalnayaPozitsiya.Y + deltaY;
                                break;
                            case 5: // Правый центр
                                newWidth = originalniyRazmer.Width + deltaX;
                                break;
                            case 6: // Нижний центр
                                newHeight = originalniyRazmer.Height + deltaY;
                                break;
                            case 7: // Левый центр
                                newWidth = originalniyRazmer.Width - deltaX;
                                newLeft = originalnayaPozitsiya.X + deltaX;
                                break;
                        }
                        
                        if (newWidth < 20) newWidth = 20;
                        if (newHeight < 20) newHeight = 20;
                        
                        if (newWidth > 0 && newHeight > 0 && originalniyRazmer.Width > 0 && originalniyRazmer.Height > 0)
                        {
                            MashtabirovatElement(elementDlyaMashtabirovaniya, newLeft, newTop, newWidth, newHeight);
                            PokazatRamuMashtabirovaniya(elementDlyaMashtabirovaniya);
                        }
                    }
                }
                else
                {
                    mashtabiruyuElement = false;
                    aktivniyMarker = null;
                    Mouse.Capture(null);
                }
                return;
            }
            
            if (peremeshayuHolst)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    var tekushayaPoz = e.GetPosition(this);
                    var deltaX = tekushayaPoz.X - nachaloPeremesheniyaHolsta.X;
                    var deltaY = tekushayaPoz.Y - nachaloPeremesheniyaHolsta.Y;
                    
                    TransformSdviga.X += deltaX;
                    TransformSdviga.Y += deltaY;
                    
                    nachaloPeremesheniyaHolsta = tekushayaPoz;
                }
                else
                {
                    peremeshayuHolst = false;
                    Mouse.Capture(null);
                    PoleDlyaRisovaniya.Cursor = Cursors.Arrow;
                }
                return;
            }
            
            if (peremeshayuElement && vybranniyElement != null)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    var tekushayaPoz = e.GetPosition(PoleDlyaRisovaniya);
                    
                    var smeshenieX = tekushayaPoz.X - nachaloPeremesheniya.X;
                    var smeshenieY = tekushayaPoz.Y - nachaloPeremesheniya.Y;
                    
                    Canvas.SetLeft(vybranniyElement, originalLeft + smeshenieX);
                    Canvas.SetTop(vybranniyElement, originalTop + smeshenieY);
                    
                    // Обновляем рамку масштабирования при перемещении
                    PokazatRamuMashtabirovaniya(vybranniyElement);
                }
                else
                {
                    peremeshayuElement = false;
                    Mouse.Capture(null);
                    vybranniyElement = null;
                }
            }
        }

        private void PoleDlyaRisovaniya_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (mashtabiruyuElement)
            {
                mashtabiruyuElement = false;
                aktivniyMarker = null;
                Mouse.Capture(null);
                if (elementDlyaMashtabirovaniya != null)
                {
                    PokazatRamuMashtabirovaniya(elementDlyaMashtabirovaniya);
                }
                return;
            }
            
            if (peremeshayuHolst)
            {
                peremeshayuHolst = false;
                Mouse.Capture(null);
                PoleDlyaRisovaniya.Cursor = Cursors.Arrow;
            }
            
            if (peremeshayuElement)
            {
                peremeshayuElement = false;
                Mouse.Capture(null);
                if (vybranniyElement != null)
                {
                    PokazatRamuMashtabirovaniya(vybranniyElement);
                }
            }
        }

        private void PoleDlyaRisovaniya_Drop(object sender, DragEventArgs e)
        {
            if (peremeshayuElement || peremeshayuHolst || !peretaskivayuIzPaneli)
            {
                e.Handled = true;
                return;
            }
            
            if (!e.Data.GetDataPresent(DataFormats.StringFormat)) return;
            
            var instrument = (string)e.Data.GetData(DataFormats.StringFormat);
            var tochkaSbrosa = e.GetPosition(PoleDlyaRisovaniya);

            UIElement element = SozdatElementPoInstrumentu(instrument, tochkaSbrosa);
            if (element != null)
            {
                DobavitNaHolst(element);
            }
        }

        private void Otmena_Click(object sender, RoutedEventArgs e)
        {
            if (PoleDlyaRisovaniya.Children.Count == 0) return;
            var element = PoleDlyaRisovaniya.Children[PoleDlyaRisovaniya.Children.Count - 1] as UIElement;
            
            // Если удаляем выбранный элемент, скрываем рамку и маркеры
            if (vybranniyElement == element || (vybranniyElement == null && vybranniyeElementy.Contains(element)))
            {
                SkrytRamuMashtabirovaniya();
                SnytVydelenie();
            }
            
            PoleDlyaRisovaniya.Children.RemoveAt(PoleDlyaRisovaniya.Children.Count - 1);
            otmenaStack.Push(element);
            vozvratStack.Clear();
        }

        private void Vozvrat_Click(object sender, RoutedEventArgs e)
        {
            if (otmenaStack.Count == 0) return;
            var element = otmenaStack.Pop();
            PoleDlyaRisovaniya.Children.Add(element);
            vozvratStack.Push(element);
            
            // Если это был выбранный элемент, обновляем рамку и маркеры
            if (vybranniyElement == element || (vybranniyElement == null && vybranniyeElementy.Contains(element)))
            {
                vybranniyElement = element;
                PokazatRamuMashtabirovaniya(element);
            }
        }

        private void DobavitNaHolst(UIElement element)
        {
            PoleDlyaRisovaniya.Children.Add(element);
            vozvratStack.Clear();
            
            // Сохраняем оригинальные размеры при добавлении элемента
            if (element != null)
            {
                // Сохраняем координаты для Line элементов сразу
                if (element is Line line)
                {
                    var lineCoords = new Dictionary<string, double>();
                    lineCoords["X1"] = line.X1;
                    lineCoords["Y1"] = line.Y1;
                    lineCoords["X2"] = line.X2;
                    lineCoords["Y2"] = line.Y2;
                    line.Tag = lineCoords;
                }
                
                // Немного задержки, чтобы элемент успел отрендериться
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var bounds = PoluchitGranitsyBezMashtaba(element);
                    if (bounds.Width > 0 && bounds.Height > 0)
                    {
                        originalnyeRazmery[element] = bounds;
                    }
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private UIElement NaytiElementNaHolste(UIElement element)
        {
            var tekushiy = element;
            while (tekushiy != null && tekushiy != PoleDlyaRisovaniya)
            {
                var roditel = VisualTreeHelper.GetParent(tekushiy) as UIElement;
                if (roditel == PoleDlyaRisovaniya)
                {
                    return tekushiy;
                }
                tekushiy = roditel;
            }
            return null;
        }

        private UIElement SozdatChelovechka()
        {
            var gruppa = new Canvas();

            //var golova = new Ellipse { Width = 30, Height = 30, Stroke = Brushes.Black, StrokeThickness = tekushayaTolschinaLinii, Fill = Brushes.Black };
            var golova = new Ellipse { Width = 30, Height = 30, Stroke = Brushes.Black, StrokeThickness = standartnayaTolschinaLinii, Fill = Brushes.Black };
            Canvas.SetLeft(golova, 50);
            Canvas.SetTop(golova, 30);

            //var telo = new Line { X1 = 65, Y1 = 60, X2 = 65, Y2 = 120, Stroke = Brushes.Black, StrokeThickness = tekushayaTolschinaLinii };
            //var rukaL = new Line { X1 = 35, Y1 = 80, X2 = 95, Y2 = 80, Stroke = Brushes.Black, StrokeThickness = tekushayaTolschinaLinii };
            //var nogaL = new Line { X1 = 65, Y1 = 120, X2 = 45, Y2 = 150, Stroke = Brushes.Black, StrokeThickness = tekushayaTolschinaLinii };
            //var nogaR = new Line { X1 = 65, Y1 = 120, X2 = 85, Y2 = 150, Stroke = Brushes.Black, StrokeThickness = tekushayaTolschinaLinii };
            var telo = new Line { X1 = 65, Y1 = 60, X2 = 65, Y2 = 120, Stroke = Brushes.Black, StrokeThickness = standartnayaTolschinaLinii };
            var rukaL = new Line { X1 = 35, Y1 = 80, X2 = 95, Y2 = 80, Stroke = Brushes.Black, StrokeThickness = standartnayaTolschinaLinii };
            var nogaL = new Line { X1 = 65, Y1 = 120, X2 = 45, Y2 = 150, Stroke = Brushes.Black, StrokeThickness = standartnayaTolschinaLinii };
            var nogaR = new Line { X1 = 65, Y1 = 120, X2 = 85, Y2 = 150, Stroke = Brushes.Black, StrokeThickness = standartnayaTolschinaLinii };
            
            gruppa.Children.Add(golova);
            gruppa.Children.Add(telo);
            gruppa.Children.Add(rukaL);
            gruppa.Children.Add(nogaL);
            gruppa.Children.Add(nogaR);

            Canvas.SetLeft(gruppa, 0);
            Canvas.SetTop(gruppa, 0);
            return gruppa;
        }

        private UIElement SozdatElementPoInstrumentu(string instrument, Point tochka)
        {
            switch (instrument)
            {
                case "aktor":
                {
                    var akter = SozdatChelovechka();
                    Canvas.SetLeft(akter, tochka.X - 65);
                    Canvas.SetTop(akter, tochka.Y - 90);
                    return akter;
                }
                case "pretsedent":
                {
                    var ellips = new Ellipse
                    {
                        Width = 120,
                        Height = 60,
                        Stroke = Brushes.Black,
                        //StrokeThickness = tekushayaTolschinaLinii,
                        StrokeThickness = standartnayaTolschinaLinii,
                        Fill = Brushes.White
                    };
                    Canvas.SetLeft(ellips, tochka.X - 60);
                    Canvas.SetTop(ellips, tochka.Y - 30);
                    return ellips;
                }
                case "sistema":
                {
                    var pryamougolnik = new Rectangle
                    {
                        Width = 240,
                        Height = 160,
                        Stroke = Brushes.Black,
                        //StrokeThickness = tekushayaTolschinaLinii,
                        StrokeThickness = standartnayaTolschinaLinii,
                        Fill = Brushes.Transparent,
                        RadiusX = 4,
                        RadiusY = 4
                    };
                    Canvas.SetLeft(pryamougolnik, tochka.X - 120);
                    Canvas.SetTop(pryamougolnik, tochka.Y - 80);
                    return pryamougolnik;
                }
                case "liniya":
                {
                    var liniya = new Line
                    {
                        X1 = tochka.X,
                        Y1 = tochka.Y,
                        X2 = tochka.X + 120,
                        Y2 = tochka.Y + 60,
                        Stroke = Brushes.Black,
                        //StrokeThickness = tekushayaTolschinaLinii
                        StrokeThickness = standartnayaTolschinaLinii
                    };
                    return liniya;
                }
                case "vklyuchit":
                {
                    var gruppa = new Canvas();
                    var liniya = new Line
                    {
                        X1 = 0,
                        Y1 = 20,
                        X2 = 130,
                        Y2 = 20,
                        Stroke = Brushes.Black,
                        //StrokeThickness = tekushayaTolschinaLinii,
                        StrokeThickness = standartnayaTolschinaLinii,
                        StrokeDashArray = new DoubleCollection { 5, 3 }
                    };
                    var strelka = new System.Windows.Shapes.Polygon
                    {
                        Points = new PointCollection { new Point(140, 20), new Point(130, 16), new Point(130, 24) },
                        Fill = Brushes.Black
                    };
                    var tekst = new TextBlock { Text = "<<include>>", Background = Brushes.LightYellow, FontSize = 11 };
                    Canvas.SetLeft(tekst, 45);
                    Canvas.SetTop(tekst, 2);
                    gruppa.Children.Add(liniya);
                    gruppa.Children.Add(strelka);
                    gruppa.Children.Add(tekst);
                    Canvas.SetLeft(gruppa, tochka.X - 70);
                    Canvas.SetTop(gruppa, tochka.Y - 20);
                    return gruppa;
                }
                case "rasshirit":
                {
                    var gruppa = new Canvas();
                    var liniya = new Line
                    {
                        X1 = 0,
                        Y1 = 20,
                        X2 = 130,
                        Y2 = 20,
                        Stroke = Brushes.Black,
                        //StrokeThickness = tekushayaTolschinaLinii,
                        StrokeThickness = standartnayaTolschinaLinii,
                        StrokeDashArray = new DoubleCollection { 5, 3 }
                    };
                    var strelka = new System.Windows.Shapes.Polygon
                    {
                        Points = new PointCollection { new Point(140, 20), new Point(130, 16), new Point(130, 24) },
                        Fill = Brushes.Black
                    };
                    var tekst = new TextBlock { Text = "<<extend>>", Background = Brushes.LightYellow, FontSize = 11 };
                    Canvas.SetLeft(tekst, 45);
                    Canvas.SetTop(tekst, 2);
                    gruppa.Children.Add(liniya);
                    gruppa.Children.Add(strelka);
                    gruppa.Children.Add(tekst);
                    Canvas.SetLeft(gruppa, tochka.X - 70);
                    Canvas.SetTop(gruppa, tochka.Y - 20);
                    return gruppa;
                }
                case "obobshenie":
                {
                    var put = new Path
                    {
                        Stroke = Brushes.Black,
                        //StrokeThickness = tekushayaTolschinaLinii,
                        StrokeThickness = standartnayaTolschinaLinii,
                        Fill = Brushes.White,
                        Data = Geometry.Parse("M 0 20 L 100 20 L 100 10 L 120 20 L 100 30 L 100 20")
                    };
                    Canvas.SetLeft(put, tochka.X - 60);
                    Canvas.SetTop(put, tochka.Y - 20);
                    return put;
                }
                case "tekst":
                {
                    var blokTeksta = new TextBlock { Text = "Текст", FontSize = 16, Foreground = Brushes.Black };
                    Canvas.SetLeft(blokTeksta, tochka.X - 20);
                    Canvas.SetTop(blokTeksta, tochka.Y - 10);
                    return blokTeksta;
                }
                default:
                    return null;
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (MainTabControl == null) return;
            
            if (MainTabControl.Items.Count > 0)
            {
                MainTabControl.SelectedItem = MainTabControl.Items[0];
                if (MainTabControl != null)
                {
                    MainTabControl.Visibility = Visibility.Visible;
                }
                if (CanvasContent != null)
                {
                    CanvasContent.Visibility = Visibility.Collapsed;
                }
                
                var leftPanel1 = FindName("LeftPanel") as Border;
                if (leftPanel1 != null)
                {
                    leftPanel1.Visibility = Visibility.Collapsed;
                }
                
                if (HelpButton != null)
                {
                    HelpButton.Background = new SolidColorBrush(Color.FromRgb(205, 133, 63));
                    HelpButton.Foreground = new SolidColorBrush(Color.FromRgb(43, 43, 43));
                }
                return;
            }
            
            TabItem helpTab = new TabItem();
            helpTab.Header = "";
            
            Grid helpContainer = new Grid { Background = new SolidColorBrush(Color.FromRgb(43, 43, 43)) };
            
            ScrollViewer scrollViewer = new ScrollViewer 
            { 
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, 
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, 
                Background = new SolidColorBrush(Color.FromRgb(43, 43, 43)),
                FlowDirection = FlowDirection.LeftToRight
            };
            
            scrollViewer.Loaded += (s, evt) =>
            {
                scrollViewer.ApplyTemplate();
                var scrollBar = scrollViewer.Template?.FindName("PART_VerticalScrollBar", scrollViewer) as ScrollBar;
                if (scrollBar != null)
                {
                    scrollBar.Style = (Style)FindResource("HelpScrollBarStyle");
                    scrollBar.HorizontalAlignment = HorizontalAlignment.Right;
                    scrollBar.FlowDirection = FlowDirection.LeftToRight;
                }
            };
            
            var interFont = new FontFamily("Inter");
            var textColor = new SolidColorBrush(Color.FromRgb(220, 220, 220));
            
            StackPanel content = new StackPanel { Margin = new Thickness(270, 60, 50, 80) };
            content.Children.Add(new TextBlock { Text = "Справка Use Case App", FontFamily = interFont, FontSize = 36, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 30), LineHeight = 44 });
            content.Children.Add(new TextBlock { Text = "Раздел предоставляет полное описание функциональных возможностей, структуры и элементов управления приложения «Use Case App». Документация предназначена для ознакомления пользователей с архитектурой проекта и эффективного использования всего инструментария.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 40), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "Структура приложения", FontFamily = interFont, FontSize = 28, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 20) });
            content.Children.Add(new TextBlock { Text = "Приложение состоит из трех основных логических и визуальных модулей:", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "1. Панель приложения — Верхняя секция интерфейса, содержащая главное меню и элементы управления проектом. Обеспечивает доступ к операциям с файлами, настройкам параметров и справочной информации.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "2. Панель инструментов — Боковая панель, содержащая набор графических элементов и функций для построения диаграмм. Включает основные сущности (Акторы, Прецеденты) и отношения (Ассоциации, Включения, Расширения, Обобщения).", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "3. Рабочее пространство — Центральная область интерфейса, предназначенная для визуального проектирования диаграмм использования. Представляет собой холст с координатной сеткой для точного позиционирования элементов.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 40), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            
            content.Children.Add(new TextBlock { Text = "1. Панель приложения", FontFamily = interFont, FontSize = 28, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 20) });
            content.Children.Add(new TextBlock { Text = "Панель приложения состоит из:", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "1. Файл", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Medium, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 8) });
            content.Children.Add(new TextBlock { Text = "2. Помощь", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Medium, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 8) });
            content.Children.Add(new TextBlock { Text = "3. Свернуть", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Medium, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 8) });
            content.Children.Add(new TextBlock { Text = "4. Развернуть", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Medium, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 8) });
            content.Children.Add(new TextBlock { Text = "5. Закрыть", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Medium, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 20) });
            content.Children.Add(new TextBlock { Text = "1. Файл — Данный раздел содержит базовый набор операций для управления жизненным циклом проекта. Функции, объединенные в этой категории, предоставляют пользователю возможности по созданию, открытию и сохранению рабочих документов в среде моделирования, обеспечивая эффективное взаимодействие с файловой системой и целостность данных.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "Окно, подразделяется на следующие разделы:", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "1.1. Новый файл — Функция создания нового файла проекта. Инициирует процесс формирования пустого рабочего документа в среде моделирования.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "1.2. Открыть — Функция импорта существующего файла проекта. Открывает диалоговое окно выбора для загрузки и последующего редактирования ранее сохранённых данных.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "1.3. Сохранить — Функция сохранения текущего состояния проекта. Обеспечивает запись всех внесённых изменений в исходный файл без изменения его местоположения и формата.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "1.4. Сохранить как — Функция экспорта проекта в новый файл. Открывает диалоговое окно для выбора местоположения, формата и имени сохраняемого файла, позволяя создать его копию или изменить параметры хранения.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 20), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "2. Помощь — предоставляет полную справочную информацию о функциональных возможностях, интерфейсе и методах работы с приложением «Use-Case App». Предназначен для оперативного получения пользователями сведений о работе с проектом.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "3. Свернуть — Скрывает приложение в панель задач. Все процессы продолжают работать в фоновом режиме.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "4. Развернуть — Переводит приложение в полноэкранный режим для максимального использования рабочей области.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "5. Закрыть — Завершает работу приложения с автоматической проверкой несохраненных изменений.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 40), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "2. Инструменты", FontFamily = interFont, FontSize = 28, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 20) });
            content.Children.Add(new TextBlock { Text = "1. Актор — роль внешнего субъекта (пользователя или системы), взаимодействующего с моделируемой системой для достижения целей.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "2. Прецедент — законченная последовательность действий системы, предоставляющая актору измеримый и ценный результат.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "3. Система — граница, отделяющая внутреннюю функциональность (прецеденты) от внешних взаимодействующих лиц (акторов).", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "4. Линия связи — отношение взаимодействия, обозначающее участие актора в выполнении прецедента.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "5. Отношение «Включить» — обязательная зависимость, при которой сценарий одного прецедента является неотъемлемой частью сценария другого.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "6. Отношение «Расширить» — опциональная зависимость, добавляющая в базовый прецедент дополнительное поведение при определённых условиях.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "7. Отношение «Обобщение» — связь «родитель-потомок», при которой дочерний элемент наследует свойства и поведение родительского.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "8. Текст — элемент для нанесения наименований и пояснительных надписей на диаграмму.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 20), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "• Сетка — инструмент, предназначенный для активации и деактивации координатной сетки в области рабочего поля.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "• Толщина линии — параметр, регулирующий толщину визуального отображения линий элементов диаграммы.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 40), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "3. Рабочее пространство", FontFamily = interFont, FontSize = 28, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 20) });
            content.Children.Add(new TextBlock { Text = "1. Панель масштабирования", FontFamily = interFont, FontSize = 22, FontWeight = FontWeights.Medium, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 15) });
            content.Children.Add(new TextBlock { Text = "1.1. Приближение (+) — Увеличивает масштаб отображения рабочей области для детального просмотра и редактирования элементов диаграммы.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "1.2. Отдаление (-) — Уменьшает масштаб отображения рабочей области для общего обзора структуры диаграммы.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 20), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "2. Панель клавишей Undo / Redo", FontFamily = interFont, FontSize = 22, FontWeight = FontWeights.Medium, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 15) });
            content.Children.Add(new TextBlock { Text = "2.1. Отмена (Undo) — Отменяет последнее выполненное действие. Позволяет последовательно откатывать внесенные изменения.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            content.Children.Add(new TextBlock { Text = "2.2. Повтор (Redo) — Восстанавливает ранее отмененное действие. Доступна после использования функции отмены.", FontFamily = interFont, FontSize = 18, FontWeight = FontWeights.Regular, Foreground = textColor, Margin = new Thickness(0, 0, 0, 0), TextWrapping = TextWrapping.Wrap, LineHeight = 28 });
            scrollViewer.Content = content;
            
            Border closeButtonBorder = new Border
            {
                Width = 32,
                Height = 32,
                Background = new SolidColorBrush(Color.FromRgb(205, 133, 63)),
                CornerRadius = new CornerRadius(6),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 20, 20, 0),
                Cursor = Cursors.Hand,
                SnapsToDevicePixels = true,
                Child = new TextBlock 
                { 
                    Text = "✕", 
                    FontFamily = interFont,
                    FontSize = 16, 
                    FontWeight = FontWeights.Bold, 
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    RenderTransformOrigin = new Point(0.5, 0.5)
                }
            };
            
            ColorAnimation hoverEnterAnimation = new ColorAnimation
            {
                To = Color.FromRgb(218, 165, 32),
                Duration = new Duration(TimeSpan.FromMilliseconds(200))
            };
            
            ColorAnimation hoverLeaveAnimation = new ColorAnimation
            {
                To = Color.FromRgb(205, 133, 63),
                Duration = new Duration(TimeSpan.FromMilliseconds(200))
            };
            
            closeButtonBorder.MouseEnter += (s, args) =>
            {
                var brush = closeButtonBorder.Background as SolidColorBrush;
                if (brush != null)
                {
                    brush.BeginAnimation(SolidColorBrush.ColorProperty, hoverEnterAnimation);
                }
            };
            
            closeButtonBorder.MouseLeave += (s, args) =>
            {
                var brush = closeButtonBorder.Background as SolidColorBrush;
                if (brush != null)
                {
                    brush.BeginAnimation(SolidColorBrush.ColorProperty, hoverLeaveAnimation);
                }
            };
            
            closeButtonBorder.MouseLeftButtonDown += (s, args) =>
            {
                MainTabControl.Items.Remove(helpTab);
                if (MainTabControl.Items.Count == 0)
                {
                    MainTabControl.Visibility = Visibility.Collapsed;
                    CanvasContent.Visibility = Visibility.Visible;
                    var leftPanel = FindName("LeftPanel") as Border;
                    if (leftPanel != null)
                    {
                        leftPanel.Visibility = Visibility.Visible;
                    }
                }
                if (HelpButton != null)
                {
                    HelpButton.Background = Brushes.Transparent;
                    HelpButton.Foreground = Brushes.White;
                }
            };
            
            helpContainer.Children.Add(scrollViewer);
            helpContainer.Children.Add(closeButtonBorder);
            
            helpTab.Content = helpContainer;
            
            MainTabControl.Items.Add(helpTab);
            MainTabControl.SelectedItem = helpTab;
            if (MainTabControl != null)
            {
                MainTabControl.Visibility = Visibility.Visible;
            }
            if (CanvasContent != null)
            {
                CanvasContent.Visibility = Visibility.Collapsed;
            }
            
            var leftPanel2 = FindName("LeftPanel") as Border;
            if (leftPanel2 != null)
            {
                leftPanel2.Visibility = Visibility.Collapsed;
            }
            
            if (HelpButton != null)
            {
                HelpButton.Background = new SolidColorBrush(Color.FromRgb(205, 133, 63));
                HelpButton.Foreground = new SolidColorBrush(Color.FromRgb(43, 43, 43));
            }
        }

        private void CloseTab_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button == null) return;
            
            var tabItem = FindParent<TabItem>(button);
            if (tabItem != null)
            {
                if (HelpButton != null)
                {
                    HelpButton.Background = Brushes.Transparent;
                    HelpButton.Foreground = Brushes.White;
                }
                MainTabControl.Items.Remove(tabItem);
                
                if (MainTabControl.Items.Count == 0)
                {
                    if (MainTabControl != null)
                    {
                        MainTabControl.Visibility = Visibility.Collapsed;
                    }
                    if (CanvasContent != null)
                    {
                        CanvasContent.Visibility = Visibility.Visible;
                    }
                    
                    var leftPanel3 = FindName("LeftPanel") as Border;
                    if (leftPanel3 != null)
                    {
                        leftPanel3.Visibility = Visibility.Visible;
                    }
                }
            }
        }

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            if (parent == null) return null;
            if (parent is T) return parent as T;
            return FindParent<T>(parent);
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MainTabControl == null) return;
            
            if (MainTabControl.SelectedItem is TabItem && HelpButton != null)
            {
                HelpButton.Background = new SolidColorBrush(Color.FromRgb(205, 133, 63));
                HelpButton.Foreground = new SolidColorBrush(Color.FromRgb(43, 43, 43));
            }
            else if (MainTabControl.Items.Count == 0)
            {
                if (MainTabControl != null)
                {
                    MainTabControl.Visibility = Visibility.Collapsed;
                }
                if (CanvasContent != null)
                {
                    CanvasContent.Visibility = Visibility.Visible;
                }
                
                var leftPanel = FindName("LeftPanel") as Border;
                if (leftPanel != null)
                {
                    leftPanel.Visibility = Visibility.Visible;
                }
                
                if (HelpButton != null)
                {
                    HelpButton.Background = Brushes.Transparent;
                    HelpButton.Foreground = Brushes.White;
                }
            }
        }

        private void FileButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void FileContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            ApplyBlurToWindow();
            if (FileButton != null)
            {
                FileButton.Background = new SolidColorBrush(Color.FromRgb(205, 133, 63));
                FileButton.Foreground = new SolidColorBrush(Color.FromRgb(43, 43, 43));
            }
        }

        private void FileContextMenu_Closed(object sender, RoutedEventArgs e)
        {
            RemoveBlurFromWindow();
            if (FileButton != null)
            {
                FileButton.Background = Brushes.Transparent;
                FileButton.Foreground = Brushes.White;
            }
        }

        private void ApplyBlurToWindow()
        {
            var mainGrid = FindName("MainContentGrid") as Grid;
            if (mainGrid != null)
            {
                var row1Content = mainGrid.Children.Cast<FrameworkElement>()
                    .FirstOrDefault(e => Grid.GetRow(e) == 1);
                if (row1Content != null)
                {
                    row1Content.Effect = new BlurEffect
                    {
                        Radius = 10
                    };
                }

                var row0Border = mainGrid.Children.Cast<FrameworkElement>()
                    .FirstOrDefault(e => Grid.GetRow(e) == 0);
                if (row0Border != null)
                {
                    ApplyBlurToHeader(row0Border, FileButton);
                }
            }
        }

        private void ApplyBlurToHeader(FrameworkElement headerElement, FrameworkElement excludeButton)
        {
            var grid = FindVisualChild<Grid>(headerElement);
            if (grid != null)
            {
                foreach (FrameworkElement child in grid.Children.OfType<FrameworkElement>())
                {
                    if (child is StackPanel stackPanel && stackPanel.Orientation == Orientation.Horizontal)
                    {
                        if (stackPanel.HorizontalAlignment == HorizontalAlignment.Left)
                        {
                            ApplyBlurToLeftPanel(stackPanel, excludeButton);
                        }
                        else if (stackPanel.HorizontalAlignment == HorizontalAlignment.Right)
                        {
                            stackPanel.Effect = new BlurEffect { Radius = 10 };
                        }
                    }
                }
            }
        }

        private void ApplyBlurToLeftPanel(StackPanel leftPanel, FrameworkElement excludeButton)
        {
            foreach (FrameworkElement child in leftPanel.Children.OfType<FrameworkElement>())
            {
                if (child is Border border && child != excludeButton)
                {
                    border.Effect = new BlurEffect { Radius = 10 };
                }
                else if (child is Button button && button != excludeButton)
                {
                    button.Effect = new BlurEffect { Radius = 10 };
                }
            }
        }

        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                {
                    return result;
                }
                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        private void RemoveBlurFromWindow()
        {
            var mainGrid = FindName("MainContentGrid") as Grid;
            if (mainGrid != null)
            {
                RemoveBlurFromElement(mainGrid);
            }
        }

        private void RemoveBlurFromElement(FrameworkElement element)
        {
            if (element != null)
            {
                element.Effect = null;

                if (element is Panel panel)
                {
                    foreach (FrameworkElement child in panel.Children.OfType<FrameworkElement>())
                    {
                        RemoveBlurFromElement(child);
                    }
                }
                else
                {
                    for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                    {
                        var child = VisualTreeHelper.GetChild(element, i) as FrameworkElement;
                        if (child != null)
                        {
                            RemoveBlurFromElement(child);
                        }
                    }
                }
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

        private void NoviyFayl_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция 'Новый файл' пока не реализована", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Otkryt_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция 'Открыть' пока не реализована", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Sohranit_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция 'Сохранить' пока не реализована", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SohranitKak_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Функция 'Сохранить как' пока не реализована", "Информация", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void DecreaseZoom_Click(object sender, MouseButtonEventArgs e)
        {
            if (PolzunokMashtaba != null && PolzunokMashtaba.Value > PolzunokMashtaba.Minimum)
            {
                PolzunokMashtaba.Value = Math.Max(PolzunokMashtaba.Minimum, PolzunokMashtaba.Value - 5);
            }
        }

        private void IncreaseZoom_Click(object sender, MouseButtonEventArgs e)
        {
            if (PolzunokMashtaba != null && PolzunokMashtaba.Value < PolzunokMashtaba.Maximum)
            {
                PolzunokMashtaba.Value = Math.Min(PolzunokMashtaba.Maximum, PolzunokMashtaba.Value + 5);
            }
        }

    }
}
