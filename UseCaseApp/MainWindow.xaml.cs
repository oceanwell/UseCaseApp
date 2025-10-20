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

namespace UseCaseApp
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
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new System.Windows.Media.Animation.QuadraticEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
            };
            
            var animatsiyaY = new System.Windows.Media.Animation.DoubleAnimation
            {
                To = mashtab,
                Duration = TimeSpan.FromMilliseconds(150),
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
    }
}
