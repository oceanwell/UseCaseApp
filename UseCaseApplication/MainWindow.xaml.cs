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

namespace UseCaseApplication
{
    public partial class MainWindow : Window
    {
        private readonly Stack<UIElement> otmenaStack = new Stack<UIElement>();
        private readonly Stack<UIElement> vozvratStack = new Stack<UIElement>();
        private double tekushayaTolschinaLinii = 2.0;
        
        private Point tochkaNachalaPeretaskivaniya;
        private Button istochnikKnopki;
        private bool peretaskivayuIzPaneli;
        
        private UIElement vybranniyElement;
        private List<UIElement> vybranniyeElementy = new List<UIElement>();
        private Point nachaloPeremesheniya;
        private bool peremeshayuElement;
        private double originalLeft;
        private double originalTop;
        
        private bool peremeshayuHolst;
        private Point nachaloPeremesheniyaHolsta;
        

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
            if (PoleDlyaRisovaniya == null) return;
            foreach (var element in PoleDlyaRisovaniya.Children.OfType<Shape>())
            {
                element.StrokeThickness = tekushayaTolschinaLinii;
            }
        }

        private void PoleDlyaRisovaniya_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (peretaskivayuIzPaneli)
            {
                return;
            }
            
            var element = e.OriginalSource as UIElement;
            
            if (element == PoleDlyaRisovaniya || element == FonSetki)
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
            }
        }
        
        private void VydelitElement(UIElement element)
        {
            if (element is Shape forma)
            {
                forma.Stroke = Brushes.DodgerBlue;
                forma.StrokeThickness = 2;
            }
            else if (element is Canvas canvas)
            {
                foreach (var docherniy in canvas.Children.OfType<Shape>())
                {
                    docherniy.Stroke = Brushes.DodgerBlue;
                    docherniy.StrokeThickness = 2;
                }
            }
        }
        
        private void SnytVydelenie()
        {
            foreach (var element in vybranniyeElementy.ToList())
            {
                if (element is Shape forma)
                {
                    forma.Stroke = Brushes.Black;
                    forma.StrokeThickness = tekushayaTolschinaLinii;
                }
                else if (element is Canvas canvas)
                {
                    foreach (var docherniy in canvas.Children.OfType<Shape>())
                    {
                        docherniy.Stroke = Brushes.Black;
                        docherniy.StrokeThickness = tekushayaTolschinaLinii;
                    }
                }
            }
            vybranniyeElementy.Clear();
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
                vybranniyElement = null;
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
        }

        private void DobavitNaHolst(UIElement element)
        {
            PoleDlyaRisovaniya.Children.Add(element);
            vozvratStack.Clear();
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

            var golova = new Ellipse { Width = 30, Height = 30, Stroke = Brushes.Black, StrokeThickness = tekushayaTolschinaLinii, Fill = Brushes.Black };
            Canvas.SetLeft(golova, 50);
            Canvas.SetTop(golova, 30);

            var telo = new Line { X1 = 65, Y1 = 60, X2 = 65, Y2 = 120, Stroke = Brushes.Black, StrokeThickness = tekushayaTolschinaLinii };
            var rukaL = new Line { X1 = 35, Y1 = 80, X2 = 95, Y2 = 80, Stroke = Brushes.Black, StrokeThickness = tekushayaTolschinaLinii };
            var nogaL = new Line { X1 = 65, Y1 = 120, X2 = 45, Y2 = 150, Stroke = Brushes.Black, StrokeThickness = tekushayaTolschinaLinii };
            var nogaR = new Line { X1 = 65, Y1 = 120, X2 = 85, Y2 = 150, Stroke = Brushes.Black, StrokeThickness = tekushayaTolschinaLinii };

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
                        StrokeThickness = tekushayaTolschinaLinii,
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
                        StrokeThickness = tekushayaTolschinaLinii,
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
                        StrokeThickness = tekushayaTolschinaLinii
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
                        StrokeThickness = tekushayaTolschinaLinii,
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
                        StrokeThickness = tekushayaTolschinaLinii,
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
                        StrokeThickness = tekushayaTolschinaLinii,
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
            
            StackPanel content = new StackPanel { Margin = new Thickness(270, 5, 50, 50) };
            content.Children.Add(new TextBlock { Text = "Справка Use Case App", FontSize = 36, FontWeight = FontWeights.Bold, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 20), LineHeight = 34 });
            content.Children.Add(new TextBlock { Text = "Раздел предоставляет полное описание функциональных возможностей, структуры и элементов управления приложения «Use Case App». Документация предназначена для ознакомления пользователей с архитектурой проекта и эффективного использования всего инструментария.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 30), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "Структура приложения", FontSize = 24, FontWeight = FontWeights.Normal, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 15) });
            content.Children.Add(new TextBlock { Text = "Приложение состоит из трех основных логических и визуальных модулей:", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "1. Панель приложения — Верхняя секция интерфейса, содержащая главное меню и элементы управления проектом. Обеспечивает доступ к операциям с файлами, настройкам параметров и справочной информации.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "2. Панель инструментов — Боковая панель, содержащая набор графических элементов и функций для построения диаграмм. Включает основные сущности (Акторы, Прецеденты) и отношения (Ассоциации, Включения, Расширения, Обобщения).", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "3. Рабочее пространство — Центральная область интерфейса, предназначенная для визуального проектирования диаграмм использования. Представляет собой холст с координатной сеткой для точного позиционирования элементов.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 30), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "1. Панель приложение", FontSize = 24, FontWeight = FontWeights.Normal, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 15) });
            content.Children.Add(new TextBlock { Text = "Панель приложения состоит из:", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "1. Файл", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10) });
            content.Children.Add(new TextBlock { Text = "2. Помощь", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10) });
            content.Children.Add(new TextBlock { Text = "3. Свернуть", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10) });
            content.Children.Add(new TextBlock { Text = "4. Развернуть", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10) });
            content.Children.Add(new TextBlock { Text = "5. Закрыть", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 15) });
            content.Children.Add(new TextBlock { Text = "1. Файл — Данный раздел содержит базовый набор операций для управления жизненным циклом проекта. Функции, объединенные в этой категории, предоставляют пользователю возможности по созданию, открытию и сохранению рабочих документов в среде моделирования, обеспечивая эффективное взаимодействие с файловой системой и целостность данных.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "Окно, подразделяется на следующие разделы:", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "1.1. Новый файл — Функция создания нового файла проекта. Инициирует процесс формирования пустого рабочего документа в среде моделирования.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "1.2. Открыть — Функция импорта существующего файла проекта. Открывает диалоговое окно выбора для загрузки и последующего редактирования ранее сохранённых данных.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "1.3. Сохранить — Функция сохранения текущего состояния проекта. Обеспечивает запись всех внесённых изменений в исходный файл без изменения его местоположения и формата.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "1.4. Сохранить как — Функция экспорта проекта в новый файл. Открывает диалоговое окно для выбора местоположения, формата и имени сохраняемого файла, позволяя создать его копию или изменить параметры хранения.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "2. Помощь — предоставляет полную справочную информацию о функциональных возможностях, интерфейсе и методах работы с приложением «Use-Case App». Предназначен для оперативного получения пользователями сведений о работе с проектом.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "3. Свернуть — Скрывает приложение в панель задач. Все процессы продолжают работать в фоновом режиме.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "4. Развернуть — Переводит приложение в полноэкранный режим для максимального использования рабочей области.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "5. Закрыть — Завершает работу приложения с автоматической проверкой несохраненных изменений.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 30), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "2. Инструменты", FontSize = 24, FontWeight = FontWeights.Normal, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 15) });
            content.Children.Add(new TextBlock { Text = "1. Актор — роль внешнего субъекта (пользователя или системы), взаимодействующего с моделируемой системой для достижения целей.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "2. Прецедент — законченная последовательность действий системы, предоставляющая актору измеримый и ценный результат.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "3. Система — граница, отделяющая внутреннюю функциональность (прецеденты) от внешних взаимодействующих лиц (акторов).", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "4. Линия связи — отношение взаимодействия, обозначающее участие актора в выполнении прецедента.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "5. Отношение «Включить» — обязательная зависимость, при которой сценарий одного прецедента является неотъемлемой частью сценария другого.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "6. Отношение «Расширить» — опциональная зависимость, добавляющая в базовый прецедент дополнительное поведение при определённых условиях.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "7. Отношение «Обобщение» — связь «родитель-потомок», при которой дочерний элемент наследует свойства и поведение родительского.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "8. Текст — элемент для нанесения наименований и пояснительных надписей на диаграмму.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "• Сетка — инструмент, предназначенный для активации и деактивации координатной сетки в области рабочего поля.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "• Толщина линии — параметр, регулирующий толщину визуального отображения линий элементов диаграммы.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 30), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "3. Рабочее пространство", FontSize = 24, FontWeight = FontWeights.Normal, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 15) });
            content.Children.Add(new TextBlock { Text = "1. Панель масштабирования", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10) });
            content.Children.Add(new TextBlock { Text = "2. Панель клавишей Undo / Redo", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 15) });
            content.Children.Add(new TextBlock { Text = "1. Панель масштабирования", FontSize = 20, FontWeight = FontWeights.Normal, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10) });
            content.Children.Add(new TextBlock { Text = "1.1. Приближение (+) — Увеличивает масштаб отображения рабочей области для детального просмотра и редактирования элементов диаграммы.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "1.2. Отдаление (-) — Уменьшает масштаб отображения рабочей области для общего обзора структуры диаграммы.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 15), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "2. Панель клавишей Undo / Redo", FontSize = 20, FontWeight = FontWeights.Normal, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10) });
            content.Children.Add(new TextBlock { Text = "2.1. Отмена (Undo) — Отменяет последнее выполненное действие. Позволяет последовательно откатывать внесенные изменения.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
            content.Children.Add(new TextBlock { Text = "2.2. Повтор (Redo) — Восстанавливает ранее отмененное действие. Доступна после использования функции отмены.", FontSize = 18, FontWeight = FontWeights.Regular, Foreground = Brushes.White, Margin = new Thickness(0, 0, 0, 0), TextWrapping = TextWrapping.Wrap });
            scrollViewer.Content = content;
            
            Border closeButtonBorder = new Border
            {
                Width = 24,
                Height = 24,
                Background = new SolidColorBrush(Color.FromRgb(205, 133, 63)),
                CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                Child = new TextBlock 
                { 
                    Text = "✕", 
                    FontSize = 14, 
                    FontWeight = FontWeights.Bold, 
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                }
            };
            
            closeButtonBorder.MouseEnter += (s, args) =>
            {
                closeButtonBorder.Background = new SolidColorBrush(Color.FromRgb(218, 165, 32));
            };
            
            closeButtonBorder.MouseLeave += (s, args) =>
            {
                closeButtonBorder.Background = new SolidColorBrush(Color.FromRgb(205, 133, 63));
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
