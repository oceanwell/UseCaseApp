using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
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
using System.Windows.Markup;
using Microsoft.Win32;
using ShapesPath = System.Windows.Shapes.Path;

namespace UseCaseApplication
{
    public partial class MainWindow : Window
    {
        private readonly Stack<UIElement> otmenaStack = new Stack<UIElement>();
        private readonly Stack<UIElement> vozvratStack = new Stack<UIElement>();
        private const double standartnayaTolschinaLinii = 1.0;
        private const string TagPolzovatelskogoTeksta = "uca-user-text";
        private double tekushayaTolschinaLinii = 2.0;
        private double tekushiyMashtab = 1.0;

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

        // Переменные для точек изгиба линий
        private List<Border> markeriIzgiba;
        private Polyline tekushayaLiniyaDlyaIzgiba;
        private int aktivnayaTochkaIzgiba = -1;
        private bool peremeshayuTochkuIzgiba;

        private string tekushiyPutFayla;
        private bool estNesokhrannyeIzmeneniya;
        private bool blokirovatOtslezhivanieIzmeneniy;
        private bool proiskhodiloPeremeshenieElementa;
        private bool proiskhodiloMashtabirovanieElementa;
        private readonly Dictionary<Line, LineCoordinates> originalnyeKoordinatyLinij = new Dictionary<Line, LineCoordinates>();
        private ScaleTransform setkaScaleTransform;
        private TranslateTransform setkaTranslateTransform;
        private DrawingBrush individualnyySetochnyyBrush;

        // Храним прикрепленные стрелки: стрелка -> (начало, конец)
        private Dictionary<UIElement, Tuple<UIElement, UIElement>> prikreplennyeStrelki = new Dictionary<UIElement, Tuple<UIElement, UIElement>>();
        // Храним точки привязки: (стрелка, индекс точки) -> (объект, относительная позиция на контуре)
        // Относительная позиция: для прямоугольника - (0-1 по ширине, 0-1 по высоте), для эллипса - угол в радианах
        private Dictionary<Tuple<UIElement, int>, Tuple<UIElement, Point>> tochkiPrikrepleniya = new Dictionary<Tuple<UIElement, int>, Tuple<UIElement, Point>>();
        private const double RadiusPrikrepleniya = 200; // Увеличенный радиус для полного охвата объектов

        // Подсветка объектов при приближении стрелки
        private List<Border> podsvetkiObektov = new List<Border>();

        // Редактирование текста
        private TextBox aktivnyTextovyEditor;
        private TextBlock redaktiruemyTextovyElement;


        public MainWindow()
        {
            InitializeComponent();

            TekstTolschiny.Text = tekushayaTolschinaLinii.ToString();
            Closing += MainWindow_Closing;
            Loaded += MainWindow_Loaded;
            MarkDocumentClean();
        }


        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            NastroitSetku();
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
            if (mashtab <= 0)
            {
                mashtab = 0.01;
            }
            tekushiyMashtab = mashtab;

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
            if (setkaScaleTransform != null)
            {
                var animXForGrid = animatsiyaX.Clone();
                var animYForGrid = animatsiyaY.Clone();
                setkaScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animXForGrid);
                setkaScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animYForGrid);
            }
            MetkaMashtaba.Text = $"{(int)e.NewValue}%";
            ObnovitMashtabTeksta();
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

            MarkDocumentDirty();
        }

        private void NastroitSetku()
        {
            if (FonSetki == null)
            {
                return;
            }

            if (individualnyySetochnyyBrush == null)
            {
                var bazovyyBrush = TryFindResource("GridBrush") as DrawingBrush;
                if (bazovyyBrush == null)
                {
                    return;
                }

                individualnyySetochnyyBrush = bazovyyBrush.Clone();
                setkaScaleTransform = new ScaleTransform(1, 1); // Фиксированный масштаб 1:1
                setkaTranslateTransform = new TranslateTransform(0, 0);

                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(setkaScaleTransform);
                transformGroup.Children.Add(setkaTranslateTransform);
                individualnyySetochnyyBrush.Transform = transformGroup;
            }

            FonSetki.Fill = individualnyySetochnyyBrush;
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
            bool byliIzmeneniya = false;
            foreach (var element in vybranniyeElementy.ToList())
            {
                if (element is Shape forma)
                {
                    forma.StrokeThickness = tekushayaTolschinaLinii;
                    originalnyeTolschiny[element] = tekushayaTolschinaLinii;
                    byliIzmeneniya = true;
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
                        byliIzmeneniya = true;
                    }
                }
            }

            if (byliIzmeneniya)
            {
                MarkDocumentDirty();
            }
        }

        private void PoleDlyaRisovaniya_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (peretaskivayuIzPaneli)
            {
                return;
            }

            if (aktivnyTextovyEditor != null)
            {
                var source = e.OriginalSource as DependencyObject;
                if (!IstochnikVnutriAktivnogoRedaktora(source))
                {
                    ZavershitRedaktirovanieTeksta(true);
                }
            }

            // Проверяем, не кликнули ли мы на маркер масштабирования или изгиба
            if (e.OriginalSource is Border marker)
            {
                if (markeriMashtaba != null && markeriMashtaba.Contains(marker))
                {
                    return; // Маркер масштабирования обработает событие сам
                }
                if (markeriIzgiba != null && markeriIzgiba.Contains(marker))
                {
                    return; // Маркер изгиба обработает событие сам
                }
            }

            var element = e.OriginalSource as UIElement;

            if (element == PoleDlyaRisovaniya || element == FonSetki || element == ramkaVydeleniya || element == HolstSoderzhanie)
            {
                SnytVydelenie();
                peremeshayuHolst = true;
                nachaloPeremesheniyaHolsta = e.GetPosition(this);
                Mouse.Capture(PoleDlyaRisovaniya);
                PoleDlyaRisovaniya.Cursor = Cursors.Hand;
                return;
            }

            var roditelskiyElement = NaytiElementNaHolste(element);

            // Если кликнули на линию или полилинию, проверяем, нужно ли добавить новую точку изгиба
            if ((roditelskiyElement is Line || roditelskiyElement is Polyline) &&
                tekushayaLiniyaDlyaIzgiba != null &&
                vybranniyElement == roditelskiyElement)
            {
                // Добавляем новую точку изгиба при клике на линию
                var clickPos = e.GetPosition(HolstSoderzhanie);
                DobavitTochkuIzgiba(clickPos);
                e.Handled = true;
                return;
            }

            // Если кликнули на Canvas с extend/include или обобщением, проверяем, нужно ли добавить новую точку изгиба
            if (roditelskiyElement is Canvas canvas &&
                tekushayaLiniyaDlyaIzgiba != null &&
                vybranniyElement == roditelskiyElement)
            {
                // Проверяем, есть ли Polyline внутри Canvas (extend/include или обобщение)
                var polylineInCanvas = canvas.Children.OfType<Polyline>().FirstOrDefault();

                if (polylineInCanvas == tekushayaLiniyaDlyaIzgiba)
                {
                    // Добавляем новую точку изгиба при клике на Canvas
                    var clickPos = e.GetPosition(HolstSoderzhanie);
                    DobavitTochkuIzgiba(clickPos);
                    e.Handled = true;
                    return;
                }
            }



            if (roditelskiyElement != null && HolstSoderzhanie != null && HolstSoderzhanie.Children.Contains(roditelskiyElement))
            {
                bool shiftNazhat = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

                if (!shiftNazhat)
                {
                    SnytVydelenie();
                }

                vybranniyElement = roditelskiyElement;
                peremeshayuElement = true;
                nachaloPeremesheniya = e.GetPosition(HolstSoderzhanie);

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
                // Оранжевый цвет выделения (#CD853F)
                forma.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CD853F"));
                // Сохраняем текущую толщину линии при выделении
                // forma.StrokeThickness остается без изменений
            }
            else if (element is Canvas canvas)
            {
                // Оранжевый цвет выделения (#CD853F)
                var orangeColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CD853F"));
                foreach (var docherniy in canvas.Children.OfType<Shape>())
                {
                    var key = docherniy as UIElement;
                    if (key != null && !originalnyeTolschiny.ContainsKey(key))
                    {
                        originalnyeTolschiny[key] = docherniy.StrokeThickness;
                    }
                    docherniy.Stroke = orangeColor;
                    // Сохраняем текущую толщину линии при выделении
                    // docherniy.StrokeThickness остается без изменений
                }
            }

            PokazatRamuMashtabirovaniya(element);
        }

        private void SkrytRamuMashtabirovaniya()
        {
            if (ramkaVydeleniya != null && HolstSoderzhanie != null)
            {
                HolstSoderzhanie.Children.Remove(ramkaVydeleniya);
                ramkaVydeleniya = null;
            }

            if (markeriMashtaba != null)
            {
                foreach (var marker in markeriMashtaba)
                {
                    if (HolstSoderzhanie != null && HolstSoderzhanie.Children.Contains(marker))
                    {
                        HolstSoderzhanie.Children.Remove(marker);
                    }
                }
                markeriMashtaba.Clear();
            }

            SkrytMarkeriIzgiba();
        }

        private void DobavitTochkuIzgiba(Point position)
        {
            if (tekushayaLiniyaDlyaIzgiba == null) return;

            var points = tekushayaLiniyaDlyaIzgiba.Points;
            if (points == null) return;

            // Проверяем, находится ли Polyline внутри Canvas
            var parent = VisualTreeHelper.GetParent(tekushayaLiniyaDlyaIzgiba) as Canvas;
            Point relativePosition = position;

            if (parent != null && parent != HolstSoderzhanie)
            {
                // Polyline внутри Canvas - преобразуем абсолютные координаты в относительные
                var canvasLeft = Canvas.GetLeft(parent);
                var canvasTop = Canvas.GetTop(parent);
                if (double.IsNaN(canvasLeft)) canvasLeft = 0;
                if (double.IsNaN(canvasTop)) canvasTop = 0;

                relativePosition = new Point(position.X - canvasLeft, position.Y - canvasTop);
            }

            // Находим ближайший сегмент линии
            int insertIndex = 0;
            double minDistance = double.MaxValue;

            for (int i = 0; i < points.Count - 1; i++)
            {
                var p1 = points[i];
                var p2 = points[i + 1];

                // Вычисляем расстояние от точки до сегмента
                var distance = RasstoyanieDoSegmenta(relativePosition, p1, p2);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    insertIndex = i + 1;
                }
            }

            // Добавляем новую точку
            points.Insert(insertIndex, relativePosition);

            // Обновляем стрелку, если есть
            if (parent != null && parent != HolstSoderzhanie)
            {
                // Проверяем, является ли это обобщением
                var polyline = parent.Children.OfType<Polyline>().FirstOrDefault();
                if (polyline != null && (polyline.StrokeDashArray == null || polyline.StrokeDashArray.Count == 0))
                {
                    // Это обобщение
                    ObnovitStrelkuObobsheniya(parent, points);
                    PokazatMarkeriIzgibaDlyaObobsheniya(parent, tekushayaLiniyaDlyaIzgiba);
                }
                else
                {
                    // Это extend/include
                    ObnovitStrelkuDlyaCanvas(parent, points);
                    PokazatMarkeriIzgibaDlyaCanvas(parent, tekushayaLiniyaDlyaIzgiba);
                }
            }
            else
            {
                PokazatMarkeriIzgiba(tekushayaLiniyaDlyaIzgiba);
            }
            MarkDocumentDirty();
        }

        private void ObnovitStrelkuDlyaCanvas(Canvas canvas, PointCollection points)
        {
            if (canvas == null || points == null || points.Count < 2) return;

            var arrow = canvas.Children.OfType<System.Windows.Shapes.Polygon>().FirstOrDefault();
            if (arrow == null) return;

            // Получаем последнюю и предпоследнюю точки линии
            var lastPoint = points[points.Count - 1];
            var prevPoint = points[points.Count - 2];

            // Вычисляем угол направления линии
            var angle = Math.Atan2(lastPoint.Y - prevPoint.Y, lastPoint.X - prevPoint.X);

            // Размер стрелки
            var arrowLength = 10.0;
            var arrowWidth = 4.0;

            // Создаем точки стрелки: острие на конце линии, затем два угла
            arrow.Points = new PointCollection
            {
                new Point(lastPoint.X, lastPoint.Y), // Острие стрелки на конце линии
                new Point(lastPoint.X - arrowLength * Math.Cos(angle) + arrowWidth * Math.Cos(angle + Math.PI / 2),
                         lastPoint.Y - arrowLength * Math.Sin(angle) + arrowWidth * Math.Sin(angle + Math.PI / 2)),
                new Point(lastPoint.X - arrowLength * Math.Cos(angle) + arrowWidth * Math.Cos(angle - Math.PI / 2),
                         lastPoint.Y - arrowLength * Math.Sin(angle) + arrowWidth * Math.Sin(angle - Math.PI / 2))
            };

            // Обновляем позицию текста (<<extend>> или <<include>>)
            var textBlock = canvas.Children.OfType<TextBlock>()
                .FirstOrDefault(tb => tb.Text == "<<extend>>" || tb.Text == "<<include>>");

            if (textBlock != null)
            {
                // Находим центр линии (середину самого длинного сегмента или общий центр)
                Point centerPoint;
                if (points.Count == 2)
                {
                    // Для двух точек - просто середина
                    centerPoint = new Point(
                        (points[0].X + points[1].X) / 2,
                        (points[0].Y + points[1].Y) / 2
                    );
                }
                else
                {
                    // Для нескольких точек - находим самый длинный сегмент и берем его середину
                    double maxLength = 0;
                    int maxIndex = 0;
                    for (int i = 0; i < points.Count - 1; i++)
                    {
                        var dx = points[i + 1].X - points[i].X;
                        var dy = points[i + 1].Y - points[i].Y;
                        var length = Math.Sqrt(dx * dx + dy * dy);
                        if (length > maxLength)
                        {
                            maxLength = length;
                            maxIndex = i;
                        }
                    }
                    centerPoint = new Point(
                        (points[maxIndex].X + points[maxIndex + 1].X) / 2,
                        (points[maxIndex].Y + points[maxIndex + 1].Y) / 2
                    );
                }

                // Вычисляем угол для позиционирования текста перпендикулярно линии
                var segmentAngle = Math.Atan2(
                    points[points.Count - 1].Y - points[0].Y,
                    points[points.Count - 1].X - points[0].X
                );

                // Смещаем текст перпендикулярно линии. Чтобы текст всегда оставался над линией,
                // используем нормаль, повернутую на -90 градусов (в экранных координатах вверх - это уменьшение Y).
                var offsetDistance = 12.0; // Расстояние от линии
                var textX = centerPoint.X + offsetDistance * Math.Sin(segmentAngle);
                var textY = centerPoint.Y - offsetDistance * Math.Cos(segmentAngle);

                // Измеряем размер текста для центрирования
                textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var textWidth = textBlock.DesiredSize.Width;
                var textHeight = textBlock.DesiredSize.Height;

                // Устанавливаем позицию текста (центрируем)
                Canvas.SetLeft(textBlock, textX - textWidth / 2);
                Canvas.SetTop(textBlock, textY - textHeight / 2);
            }
        }

        private double RasstoyanieDoSegmenta(Point p, Point p1, Point p2)
        {
            var dx = p2.X - p1.X;
            var dy = p2.Y - p1.Y;
            var lengthSquared = dx * dx + dy * dy;

            if (lengthSquared == 0)
            {
                // Сегмент - это точка
                return Math.Sqrt((p.X - p1.X) * (p.X - p1.X) + (p.Y - p1.Y) * (p.Y - p1.Y));
            }

            var t = Math.Max(0, Math.Min(1, ((p.X - p1.X) * dx + (p.Y - p1.Y) * dy) / lengthSquared));
            var projX = p1.X + t * dx;
            var projY = p1.Y + t * dy;

            return Math.Sqrt((p.X - projX) * (p.X - projX) + (p.Y - projY) * (p.Y - projY));
        }

        private void PokazatRamuMashtabirovaniya(UIElement element)
        {
            if (element == null || HolstSoderzhanie == null || !HolstSoderzhanie.Children.Contains(element))
            {
                SkrytRamuMashtabirovaniya();
                return;
            }

            SkrytRamuMashtabirovaniya();

            // Для Line и Polyline показываем маркеры изгиба вместо маркеров масштабирования
            if (element is Line || element is Polyline)
            {
                PokazatMarkeriIzgiba(element);
                return;
            }

            // Для старых ShapesPath обобщения преобразуем в Canvas с Polyline
            if (element is ShapesPath path)
            {
                // Проверяем, является ли это обобщением (путь со стрелкой)
                var geometry = path.Data as PathGeometry;
                if (geometry != null && geometry.Figures.Count > 0)
                {
                    // Преобразуем Path в Canvas с Polyline
                    var noviyCanvas = new Canvas();
                    var points = new PointCollection();

                    // Извлекаем точки из PathGeometry
                    foreach (var figure in geometry.Figures)
                    {
                        var startPoint = figure.StartPoint;
                        points.Add(startPoint);

                        foreach (var segment in figure.Segments)
                        {
                            if (segment is LineSegment lineSegment)
                            {
                                points.Add(lineSegment.Point);
                            }
                        }
                    }

                    if (points.Count >= 2)
                    {
                        var polyline = new Polyline
                        {
                            Points = points,
                            Stroke = path.Stroke,
                            StrokeThickness = path.StrokeThickness
                        };

                        // Создаем стрелку обобщения на конце
                        var lastPoint = points[points.Count - 1];
                        var prevPoint = points[points.Count - 2];
                        var angle = Math.Atan2(lastPoint.Y - prevPoint.Y, lastPoint.X - prevPoint.X);

                        var arrow = new System.Windows.Shapes.Polygon
                        {
                            Points = new PointCollection
                            {
                                new Point(lastPoint.X, lastPoint.Y),
                                new Point(lastPoint.X - 20 * Math.Cos(angle) + 10 * Math.Cos(angle + Math.PI / 2),
                                         lastPoint.Y - 20 * Math.Sin(angle) + 10 * Math.Sin(angle + Math.PI / 2)),
                                new Point(lastPoint.X - 20 * Math.Cos(angle) + 10 * Math.Cos(angle - Math.PI / 2),
                                         lastPoint.Y - 20 * Math.Sin(angle) + 10 * Math.Sin(angle - Math.PI / 2))
                            },
                            Fill = path.Fill,
                            Stroke = path.Stroke,
                            StrokeThickness = path.StrokeThickness
                        };

                        noviyCanvas.Children.Add(polyline);
                        noviyCanvas.Children.Add(arrow);

                        // Заменяем Path на Canvas
                        var parent = VisualTreeHelper.GetParent(path) as Panel;
                        if (parent != null)
                        {
                            var left = Canvas.GetLeft(path);
                            var top = Canvas.GetTop(path);
                            if (double.IsNaN(left)) left = 0;
                            if (double.IsNaN(top)) top = 0;

                            int index = parent.Children.IndexOf(path);
                            parent.Children.RemoveAt(index);
                            parent.Children.Insert(index, noviyCanvas);

                            Canvas.SetLeft(noviyCanvas, left);
                            Canvas.SetTop(noviyCanvas, top);

                            if (vybranniyElement == path)
                            {
                                vybranniyElement = noviyCanvas;
                            }

                            // Показываем маркеры изгиба
                            PokazatMarkeriIzgibaDlyaObobsheniya(noviyCanvas, polyline);
                            return;
                        }
                    }
                }
            }

            // Для Canvas с extend/include или обобщением показываем маркеры изгиба для Polyline внутри
            if (element is Canvas canvas)
            {
                Polyline polylineInCanvas = null;
                bool isObobshenie = false;

                foreach (var child in canvas.Children)
                {
                    if (child is Polyline pl)
                    {
                        // Проверяем, есть ли стрелка (Polygon) - это может быть обобщение или extend/include
                        var hasArrow = canvas.Children.OfType<System.Windows.Shapes.Polygon>().Any();
                        if (pl.StrokeDashArray != null && pl.StrokeDashArray.Count > 0)
                        {
                            // Это extend/include
                            polylineInCanvas = pl;
                            break;
                        }
                        else if (hasArrow && (pl.StrokeDashArray == null || pl.StrokeDashArray.Count == 0))
                        {
                            // Это обобщение (Polyline без пунктира, но с Polygon стрелкой)
                            polylineInCanvas = pl;
                            isObobshenie = true;
                            break;
                        }
                    }
                    else if (child is Line line && line.StrokeDashArray != null && line.StrokeDashArray.Count > 0)
                    {
                        // Преобразуем Line в Polyline для изгиба
                        polylineInCanvas = new Polyline
                        {
                            Stroke = line.Stroke,
                            StrokeThickness = line.StrokeThickness,
                            StrokeDashArray = line.StrokeDashArray,
                            Points = new PointCollection { new Point(line.X1, line.Y1), new Point(line.X2, line.Y2) }
                        };

                        // Заменяем Line на Polyline в Canvas
                        int index = canvas.Children.IndexOf(line);
                        canvas.Children.RemoveAt(index);
                        canvas.Children.Insert(index, polylineInCanvas);
                        break;
                    }
                }

                if (polylineInCanvas != null)
                {
                    // Показываем маркеры изгиба для Polyline внутри Canvas
                    if (isObobshenie)
                    {
                        PokazatMarkeriIzgibaDlyaObobsheniya(canvas, polylineInCanvas);
                    }
                    else
                    {
                        PokazatMarkeriIzgibaDlyaCanvas(canvas, polylineInCanvas);
                    }
                    return;
                }
            }

            var bounds = PoluchitGranitsyElementa(element);
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                // Если границы не определены, используем значения по умолчанию
                bounds = new Rect(bounds.Left, bounds.Top, 120, 150);
            }

            // Создаем рамку выделения (пунктирная граница)
            var rect = new Rectangle
            {
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CD853F")),
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
            if (HolstSoderzhanie != null)
            {
                HolstSoderzhanie.Children.Add(ramkaVydeleniya);
            }

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
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CD853F")),
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

                // Подключаем обработчики масштабирования
                marker.MouseLeftButtonDown += Marker_MouseLeftButtonDown;
                marker.MouseMove += Marker_MouseMove;
                marker.MouseLeftButtonUp += Marker_MouseLeftButtonUp;

                // Сохраняем индекс маркера для быстрого доступа
                marker.Tag = i;
                marker.IsHitTestVisible = true; // Включаем реакцию на мышь

                if (HolstSoderzhanie != null)
                {
                    HolstSoderzhanie.Children.Add(marker);
                }
                markeriMashtaba.Add(marker);
            }
        }

        private void PokazatMarkeriIzgiba(UIElement element)
        {
            SkrytMarkeriIzgiba();

            Polyline polyline = null;
            PointCollection points = null;

            if (element is Polyline pl)
            {
                polyline = pl;
                points = pl.Points;
            }
            else if (element is Line line)
            {
                // Преобразуем Line в Polyline
                polyline = new Polyline
                {
                    Stroke = line.Stroke,
                    StrokeThickness = line.StrokeThickness,
                    StrokeDashArray = line.StrokeDashArray,
                    Points = new PointCollection { new Point(line.X1, line.Y1), new Point(line.X2, line.Y2) }
                };

                // Заменяем Line на Polyline
                var parent = VisualTreeHelper.GetParent(line) as Panel;
                if (parent != null)
                {
                    int index = parent.Children.IndexOf(line);
                    parent.Children.RemoveAt(index);
                    parent.Children.Insert(index, polyline);

                    if (vybranniyElement == line)
                    {
                        vybranniyElement = polyline;
                    }
                }

                points = polyline.Points;
            }

            if (polyline == null || points == null || points.Count == 0) return;

            tekushayaLiniyaDlyaIzgiba = polyline;

            SozdatMarkeriIzgiba(points, null);
        }

        private void PokazatMarkeriIzgibaDlyaCanvas(Canvas canvas, Polyline polyline)
        {
            SkrytMarkeriIzgiba();

            if (polyline == null || polyline.Points == null || polyline.Points.Count == 0) return;

            tekushayaLiniyaDlyaIzgiba = polyline;

            // Обновляем позицию стрелки перед показом маркеров
            ObnovitStrelkuDlyaCanvas(canvas, polyline.Points);

            // Получаем позицию Canvas на холсте
            var canvasLeft = Canvas.GetLeft(canvas);
            var canvasTop = Canvas.GetTop(canvas);
            if (double.IsNaN(canvasLeft)) canvasLeft = 0;
            if (double.IsNaN(canvasTop)) canvasTop = 0;

            // Создаем маркеры с учетом позиции Canvas
            SozdatMarkeriIzgiba(polyline.Points, new Point(canvasLeft, canvasTop));
        }

        private void PokazatMarkeriIzgibaDlyaObobsheniya(Canvas canvas, Polyline polyline)
        {
            SkrytMarkeriIzgiba();

            if (polyline == null || polyline.Points == null || polyline.Points.Count == 0) return;

            tekushayaLiniyaDlyaIzgiba = polyline;

            // Обновляем позицию стрелки обобщения перед показом маркеров
            ObnovitStrelkuObobsheniya(canvas, polyline.Points);

            // Получаем позицию Canvas на холсте
            var canvasLeft = Canvas.GetLeft(canvas);
            var canvasTop = Canvas.GetTop(canvas);
            if (double.IsNaN(canvasLeft)) canvasLeft = 0;
            if (double.IsNaN(canvasTop)) canvasTop = 0;

            // Создаем маркеры с учетом позиции Canvas
            SozdatMarkeriIzgiba(polyline.Points, new Point(canvasLeft, canvasTop));
        }

        private void ObnovitStrelkuObobsheniya(Canvas canvas, PointCollection points)
        {
            if (canvas == null || points == null || points.Count < 2) return;

            var arrow = canvas.Children.OfType<System.Windows.Shapes.Polygon>().FirstOrDefault();
            if (arrow == null) return;

            // Получаем последнюю и предпоследнюю точки линии
            var lastPoint = points[points.Count - 1];
            var prevPoint = points[points.Count - 2];

            // Вычисляем угол направления линии
            var angle = Math.Atan2(lastPoint.Y - prevPoint.Y, lastPoint.X - prevPoint.X);

            // Размер стрелки обобщения (треугольная, больше чем у extend/include)
            var arrowLength = 20.0;
            var arrowWidth = 10.0;

            // Создаем точки треугольной стрелки обобщения
            arrow.Points = new PointCollection
            {
                new Point(lastPoint.X, lastPoint.Y), // Острие стрелки на конце линии
                new Point(lastPoint.X - arrowLength * Math.Cos(angle) + arrowWidth * Math.Cos(angle + Math.PI / 2),
                         lastPoint.Y - arrowLength * Math.Sin(angle) + arrowWidth * Math.Sin(angle + Math.PI / 2)),
                new Point(lastPoint.X - arrowLength * Math.Cos(angle) + arrowWidth * Math.Cos(angle - Math.PI / 2),
                         lastPoint.Y - arrowLength * Math.Sin(angle) + arrowWidth * Math.Sin(angle - Math.PI / 2))
            };
        }

        private void SozdatMarkeriIzgiba(PointCollection points, Point? offset)
        {
            if (markeriIzgiba == null)
            {
                markeriIzgiba = new List<Border>();
            }

            markeriIzgiba.Clear();
            double markerSize = 8;

            // Создаем маркеры для каждой точки
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                var absoluteX = offset.HasValue ? point.X + offset.Value.X : point.X;
                var absoluteY = offset.HasValue ? point.Y + offset.Value.Y : point.Y;

                var marker = new Border
                {
                    Width = markerSize,
                    Height = markerSize,
                    Background = Brushes.LightBlue,
                    BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CD853F")),
                    BorderThickness = new Thickness(1.5),
                    Cursor = Cursors.Hand,
                    SnapsToDevicePixels = true,
                    Opacity = 1.0,
                    IsHitTestVisible = true,
                    CornerRadius = new CornerRadius(markerSize / 2),
                    ClipToBounds = false
                };

                marker.Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 3,
                    Opacity = 0.5,
                    ShadowDepth = 1
                };

                Canvas.SetLeft(marker, absoluteX - markerSize / 2);
                Canvas.SetTop(marker, absoluteY - markerSize / 2);
                Panel.SetZIndex(marker, 1001);

                marker.Tag = i;
                marker.MouseLeftButtonDown += MarkerIzgiba_MouseLeftButtonDown;
                marker.MouseLeftButtonUp += MarkerIzgiba_MouseLeftButtonUp;

                if (HolstSoderzhanie != null)
                {
                    HolstSoderzhanie.Children.Add(marker);
                }
                markeriIzgiba.Add(marker);
            }
        }

        private void SkrytMarkeriIzgiba()
        {
            if (markeriIzgiba != null)
            {
                foreach (var marker in markeriIzgiba)
                {
                    if (HolstSoderzhanie != null && HolstSoderzhanie.Children.Contains(marker))
                    {
                        HolstSoderzhanie.Children.Remove(marker);
                    }
                }
                markeriIzgiba.Clear();
            }
            tekushayaLiniyaDlyaIzgiba = null;
        }

        private void MarkerIzgiba_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (tekushayaLiniyaDlyaIzgiba == null) return;

            var border = sender as Border;
            aktivnayaTochkaIzgiba = (border?.Tag is int index) ? index : -1;
            if (aktivnayaTochkaIzgiba >= 0)
            {
                peremeshayuTochkuIzgiba = true;
                Mouse.Capture(PoleDlyaRisovaniya);
                e.Handled = true;
            }
        }

        private void MarkerIzgiba_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (peremeshayuTochkuIzgiba)
            {
                // Прикрепляем точку к подсвеченному объекту или ближайшему
                if (tekushayaLiniyaDlyaIzgiba != null && aktivnayaTochkaIzgiba >= 0)
                {
                    var points = tekushayaLiniyaDlyaIzgiba.Points;
                    if (aktivnayaTochkaIzgiba < points.Count)
                    {
                        var isPervayaTochka = aktivnayaTochkaIzgiba == 0;
                        var isPoslednyayaTochka = aktivnayaTochkaIzgiba == points.Count - 1;

                        UIElement objToAttach = null;

                        if (isPervayaTochka || isPoslednyayaTochka)
                        {
                            var parent = VisualTreeHelper.GetParent(tekushayaLiniyaDlyaIzgiba) as Canvas;
                            Point absPoint;
                            if (parent != null && parent != HolstSoderzhanie)
                            {
                                var canvasLeft = Canvas.GetLeft(parent); if (double.IsNaN(canvasLeft)) canvasLeft = 0;
                                var canvasTop = Canvas.GetTop(parent); if (double.IsNaN(canvasTop)) canvasTop = 0;
                                absPoint = new Point(points[aktivnayaTochkaIzgiba].X + canvasLeft, points[aktivnayaTochkaIzgiba].Y + canvasTop);
                            }
                            else
                            {
                                absPoint = points[aktivnayaTochkaIzgiba];
                            }
                            objToAttach = NaytiObektVDiapazone(absPoint, RadiusPrikrepleniya);
                        }

                        if (objToAttach != null)
                        {
                            // Используем позицию мыши для точной привязки
                            var mousePos = e.GetPosition(HolstSoderzhanie);
                            PrivyazatTochkuKObektu(tekushayaLiniyaDlyaIzgiba, aktivnayaTochkaIzgiba, objToAttach, mousePos);
                        }
                        else
                        {
                            OtdelitTochkuOtObekta(tekushayaLiniyaDlyaIzgiba, aktivnayaTochkaIzgiba);
                        }
                    }
                }

                peremeshayuTochkuIzgiba = false;
                aktivnayaTochkaIzgiba = -1;
                SkrytPodsvetku();
                Mouse.Capture(null);
                MarkDocumentDirty();
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
                // Для Line вычисляем размер и позицию на основе координат
                if (shape is Line line)
                {
                    // Для Line координаты X1/Y1/X2/Y2 абсолютные на Canvas
                    // Вычисляем границы линии
                    var minX = Math.Min(line.X1, line.X2);
                    var minY = Math.Min(line.Y1, line.Y2);
                    var maxX = Math.Max(line.X1, line.X2);
                    var maxY = Math.Max(line.Y1, line.Y2);

                    // Позиция - это минимальные координаты
                    left = minX;
                    top = minY;

                    // Размеры - это разница между максимальными и минимальными координатами
                    width = (maxX - minX) * scaleX;
                    height = (maxY - minY) * scaleY;
                    if (width < (maxX - minX)) width = maxX - minX;
                    if (height < (maxY - minY)) height = maxY - minY;
                }
                else if (shape is Polyline polyline)
                {
                    // Для Polyline вычисляем границы на основе всех точек
                    if (polyline.Points != null && polyline.Points.Count > 0)
                    {
                        var minX = polyline.Points.Min(p => p.X);
                        var minY = polyline.Points.Min(p => p.Y);
                        var maxX = polyline.Points.Max(p => p.X);
                        var maxY = polyline.Points.Max(p => p.Y);

                        left = minX;
                        top = minY;
                        width = (maxX - minX) * scaleX;
                        height = (maxY - minY) * scaleY;
                        if (width < (maxX - minX)) width = maxX - minX;
                        if (height < (maxY - minY)) height = maxY - minY;
                    }
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
                // Для Line вычисляем размер и позицию на основе координат
                if (shape is Line line)
                {
                    // Используем оригинальные координаты, если они сохранены
                    double x1, y1, x2, y2;
                    if (originalnyeKoordinatyLinij.TryGetValue(line, out var origCoords))
                    {
                        x1 = origCoords.X1;
                        y1 = origCoords.Y1;
                        x2 = origCoords.X2;
                        y2 = origCoords.Y2;
                    }
                    else
                    {
                        // Если оригинальные координаты не сохранены, используем текущие
                        x1 = line.X1;
                        y1 = line.Y1;
                        x2 = line.X2;
                        y2 = line.Y2;
                    }

                    // Для Line координаты X1/Y1/X2/Y2 абсолютные на Canvas
                    // Вычисляем границы линии
                    var minX = Math.Min(x1, x2);
                    var minY = Math.Min(y1, y2);
                    var maxX = Math.Max(x1, x2);
                    var maxY = Math.Max(y1, y2);

                    // Позиция - это минимальные координаты
                    left = minX;
                    top = minY;

                    // Размеры - это разница между максимальными и минимальными координатами
                    width = maxX - minX;
                    height = maxY - minY;
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
            tochkaNachalaMashtabirovaniya = e.GetPosition(HolstSoderzhanie);

            // Получаем текущие размеры элемента (с учетом масштабирования)
            var currentBounds = PoluchitGranitsyElementa(elementDlyaMashtabirovaniya);

            // Для Line элементов координаты задаются через X1/Y1/X2/Y2, а не через Canvas.GetLeft/Top
            // Поэтому используем границы из currentBounds
            double realLeft, realTop;
            if (elementDlyaMashtabirovaniya is Line)
            {
                // Для Line используем границы из PoluchitGranitsyElementa
                realLeft = currentBounds.Left;
                realTop = currentBounds.Top;
            }
            else
            {
                // Для других элементов получаем РЕАЛЬНУЮ позицию на Canvas
                realLeft = Canvas.GetLeft(elementDlyaMashtabirovaniya);
                realTop = Canvas.GetTop(elementDlyaMashtabirovaniya);
                if (double.IsNaN(realLeft)) realLeft = 0;
                if (double.IsNaN(realTop)) realTop = 0;
            }

            // Сохраняем оригинальные размеры при первом масштабировании
            if (!originalnyeRazmery.ContainsKey(elementDlyaMashtabirovaniya))
            {
                var realBounds = PoluchitGranitsyBezMashtaba(elementDlyaMashtabirovaniya);
                originalnyeRazmery[elementDlyaMashtabirovaniya] = realBounds;
            }

            // Используем текущие размеры и позицию элемента
            // Это предотвращает перемещение элемента при нажатии на маркер
            originalniyRazmer = new Rect(realLeft, realTop, currentBounds.Width, currentBounds.Height);
            originalnayaPozitsiya = new Point(realLeft, realTop);

            // Захватываем мышь на Canvas, чтобы события продолжали обрабатываться даже если курсор покинет маркер
            Mouse.Capture(PoleDlyaRisovaniya);
            e.Handled = true;
        }

        private void Marker_MouseMove(object sender, MouseEventArgs e)
        {
            // Обработка масштабирования происходит в PoleDlyaRisovaniya_MouseMove
            // Этот метод оставлен для совместимости, но логика перенесена
        }

        private void Marker_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (mashtabiruyuElement)
            {
                mashtabiruyuElement = false;
                if (aktivniyMarker != null)
                {
                    aktivniyMarker.ReleaseMouseCapture();
                }
                aktivniyMarker = null;
                Mouse.Capture(null);

                if (elementDlyaMashtabirovaniya != null && proiskhodiloMashtabirovanieElementa)
                {
                    PokazatRamuMashtabirovaniya(elementDlyaMashtabirovaniya);
                    MarkDocumentDirty();
                    proiskhodiloMashtabirovanieElementa = false;
                }
            }
        }

        private void MashtabirovatElement(UIElement element, double left, double top, double width, double height)
        {
            if (element == null || width <= 0 || height <= 0) return;

            // Сохраняем оригинальные размеры при первом масштабировании
            if (!originalnyeRazmery.ContainsKey(element))
            {
                var realBounds = PoluchitGranitsyBezMashtaba(element);
                originalnyeRazmery[element] = realBounds;
            }

            // Получаем оригинальные размеры для вычисления финального масштаба
            var baseBounds = originalnyeRazmery[element];
            if (baseBounds.Width <= 0 || baseBounds.Height <= 0) return;

            // Вычисляем финальный масштаб напрямую относительно оригинальных размеров
            // width и height - это желаемые размеры, которые передаются в функцию
            var finalScaleX = width / baseBounds.Width;
            var finalScaleY = height / baseBounds.Height;

            // Для Canvas проверяем, содержит ли он Polyline (extend/include или обобщение) - для них не масштабируем
            if (element is Canvas canvas)
            {
                // Проверяем, является ли это extend/include или обобщением
                bool isExtendIncludeOrObobshenie = false;
                foreach (var child in canvas.Children)
                {
                    if ((child is Line line && line.StrokeDashArray != null && line.StrokeDashArray.Count > 0) ||
                        (child is Polyline polyline && polyline.StrokeDashArray != null && polyline.StrokeDashArray.Count > 0))
                    {
                        isExtendIncludeOrObobshenie = true;
                        break;
                    }
                    else if (child is Polyline polyline2 && canvas.Children.OfType<System.Windows.Shapes.Polygon>().Any())
                    {
                        // Это обобщение (Polyline без пунктира, но с Polygon стрелкой)
                        isExtendIncludeOrObobshenie = true;
                        break;
                    }
                }

                // Для extend/include и обобщения не масштабируем - они изгибаются через маркеры
                if (isExtendIncludeOrObobshenie)
                {
                    // Просто обновляем позицию Canvas
                    Canvas.SetLeft(canvas, left);
                    Canvas.SetTop(canvas, top);
                    return;
                }

                // Для других Canvas (актор и т.д.) используем обычное масштабирование
                // Оригинальные размеры уже сохранены в начале функции, используем их

                // Масштабируем Canvas через RenderTransform
                var transform = canvas.RenderTransform as ScaleTransform;
                if (transform == null)
                {
                    transform = new ScaleTransform(1.0, 1.0);
                    canvas.RenderTransform = transform;
                    canvas.RenderTransformOrigin = new Point(0, 0);
                }

                transform.ScaleX = finalScaleX;
                transform.ScaleY = finalScaleY;

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
                // Для Line используем растягивание только если это не часть Canvas (extend/include уже обработаны выше)
                if (shape is Line lineForScale)
                {
                    // Сохраняем оригинальные координаты при первом изменении
                    if (!originalnyeKoordinatyLinij.ContainsKey(lineForScale))
                    {
                        originalnyeKoordinatyLinij[lineForScale] = new LineCoordinates(
                            lineForScale.X1,
                            lineForScale.Y1,
                            lineForScale.X2,
                            lineForScale.Y2
                        );
                    }

                    // Растягиваем линию к новым границам
                    var origCoords = originalnyeKoordinatyLinij[lineForScale];
                    var origWidth = Math.Abs(origCoords.X2 - origCoords.X1);
                    var origHeight = Math.Abs(origCoords.Y2 - origCoords.Y1);

                    if (origWidth > 0 || origHeight > 0)
                    {
                        // Линия всегда растягивается от одного угла прямоугольника к другому
                        // Определяем направление на основе исходных координат
                        // Сравниваем координаты начала и конца линии
                        bool goesRight = origCoords.X2 > origCoords.X1;
                        bool goesDown = origCoords.Y2 > origCoords.Y1;

                        // Если координаты равны, используем направление по другой оси
                        if (Math.Abs(origCoords.X2 - origCoords.X1) < 0.001)
                        {
                            goesRight = origCoords.Y2 > origCoords.Y1; // Вертикальная линия
                        }
                        if (Math.Abs(origCoords.Y2 - origCoords.Y1) < 0.001)
                        {
                            goesDown = origCoords.X2 > origCoords.X1; // Горизонтальная линия
                        }

                        // Определяем углы на основе направления
                        double startX, startY, endX, endY;

                        if (goesRight && goesDown)
                        {
                            // От левого верхнего к правому нижнему
                            startX = left;
                            startY = top;
                            endX = left + width;
                            endY = top + height;
                        }
                        else if (!goesRight && goesDown)
                        {
                            // От правого верхнего к левому нижнему
                            startX = left + width;
                            startY = top;
                            endX = left;
                            endY = top + height;
                        }
                        else if (goesRight && !goesDown)
                        {
                            // От левого нижнего к правому верхнему
                            startX = left;
                            startY = top + height;
                            endX = left + width;
                            endY = top;
                        }
                        else
                        {
                            // От правого нижнего к левому верхнему
                            startX = left + width;
                            startY = top + height;
                            endX = left;
                            endY = top;
                        }

                        lineForScale.X1 = startX;
                        lineForScale.Y1 = startY;
                        lineForScale.X2 = endX;
                        lineForScale.Y2 = endY;
                    }
                }
                else if (shape is ShapesPath path)
                {
                    // Для ShapesPath (обобщение) растягиваем через изменение Data вместо масштабирования
                    // Сохраняем оригинальный PathGeometry при первом изменении
                    if (!originalnyeRazmery.ContainsKey(element))
                    {
                        // Сохраняем текущие размеры
                        var currentBounds = PoluchitGranitsyBezMashtaba(path);
                        originalnyeRazmery[element] = currentBounds;
                    }

                    // Для обобщения используем масштабирование через RenderTransform
                    // так как это сложная фигура со стрелкой
                    var transform = path.RenderTransform as ScaleTransform;
                    if (transform == null)
                    {
                        transform = new ScaleTransform(1.0, 1.0, 0, 0);
                        path.RenderTransform = transform;
                        path.RenderTransformOrigin = new Point(0, 0);
                    }
                    transform.ScaleX = finalScaleX;
                    transform.ScaleY = finalScaleY;
                }
                else
                {
                    // Для Ellipse, Rectangle и других можно менять размеры напрямую
                    shape.Width = baseBounds.Width * finalScaleX;
                    shape.Height = baseBounds.Height * finalScaleY;
                }

                // Обновляем визуализацию для всех Shape
                shape.InvalidateVisual();

                // Для Line координаты уже установлены напрямую (X1, Y1, X2, Y2)
                // Для других Shape устанавливаем позицию через Canvas
                if (!(shape is Line))
                {
                    Canvas.SetLeft(shape, left);
                    Canvas.SetTop(shape, top);
                }
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

                var fontSizeScale = Math.Min(finalScaleX, finalScaleY);
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
                fe.Width = baseRect.Width * finalScaleX;
                fe.Height = baseRect.Height * finalScaleY;
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
            // Если перетаскиваем точку изгиба
            if (peremeshayuTochkuIzgiba && aktivnayaTochkaIzgiba >= 0 && tekushayaLiniyaDlyaIzgiba != null)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    var newPos = e.GetPosition(HolstSoderzhanie);
                    var points = tekushayaLiniyaDlyaIzgiba.Points;
                    if (aktivnayaTochkaIzgiba < points.Count)
                    {
                        // Проверяем, находится ли Polyline внутри Canvas
                        var parent = VisualTreeHelper.GetParent(tekushayaLiniyaDlyaIzgiba) as Canvas;
                        
                        Point finalPos = newPos;
                        // При перемещении точки изгиба не прилипаем автоматически - только при отпускании мыши
                        
                        if (parent != null && parent != HolstSoderzhanie)
                        {
                            // Polyline внутри Canvas - координаты относительные
                            var canvasLeft = Canvas.GetLeft(parent);
                            var canvasTop = Canvas.GetTop(parent);
                            if (double.IsNaN(canvasLeft)) canvasLeft = 0;
                            if (double.IsNaN(canvasTop)) canvasTop = 0;

                            var relativePos = new Point(finalPos.X - canvasLeft, finalPos.Y - canvasTop);
                            points[aktivnayaTochkaIzgiba] = relativePos;

                            // Всегда обновляем позицию стрелки при изменении любой точки
                            // Проверяем, является ли это обобщением (нет пунктира у Polyline)
                            var polyline = parent.Children.OfType<Polyline>().FirstOrDefault();
                            if (polyline != null && (polyline.StrokeDashArray == null || polyline.StrokeDashArray.Count == 0))
                            {
                                // Это обобщение
                                ObnovitStrelkuObobsheniya(parent, points);
                            }
                            else
                            {
                                // Это extend/include
                                ObnovitStrelkuDlyaCanvas(parent, points);
                            }
                        }
                        else
                        {
                            // Polyline напрямую на холсте - координаты абсолютные
                            points[aktivnayaTochkaIzgiba] = finalPos;
                        }

                        // Обновляем позицию маркера
                        if (markeriIzgiba != null && aktivnayaTochkaIzgiba < markeriIzgiba.Count)
                        {
                            var marker = markeriIzgiba[aktivnayaTochkaIzgiba];
                            Canvas.SetLeft(marker, finalPos.X - marker.Width / 2);
                            Canvas.SetTop(marker, finalPos.Y - marker.Height / 2);
                        }
                    }
                }
                return;
            }

            // Если масштабируем, обрабатываем масштабирование
            if (mashtabiruyuElement && aktivniyMarker != null && elementDlyaMashtabirovaniya != null)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    var tekushayaPoz = e.GetPosition(HolstSoderzhanie);
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

                        // Вычисляем новые размеры и позицию в зависимости от маркера
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

                        // Применяем масштабирование только если размеры валидны
                        if (newWidth > 0 && newHeight > 0 && originalniyRazmer.Width > 0 && originalniyRazmer.Height > 0)
                        {
                            MashtabirovatElement(elementDlyaMashtabirovaniya, newLeft, newTop, newWidth, newHeight);
                            PokazatRamuMashtabirovaniya(elementDlyaMashtabirovaniya);
                            proiskhodiloMashtabirovanieElementa = true;
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

                    if (TransformSdviga != null)
                    {
                        TransformSdviga.X += deltaX;
                        TransformSdviga.Y += deltaY;
                    }
                    if (setkaTranslateTransform != null)
                    {
                        setkaTranslateTransform.X += deltaX;
                        setkaTranslateTransform.Y += deltaY;
                    }

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
                    var tekushayaPoz = e.GetPosition(HolstSoderzhanie);

                    var smeshenieX = tekushayaPoz.X - nachaloPeremesheniya.X;
                    var smeshenieY = tekushayaPoz.Y - nachaloPeremesheniya.Y;

                    Canvas.SetLeft(vybranniyElement, originalLeft + smeshenieX);
                    Canvas.SetTop(vybranniyElement, originalTop + smeshenieY);

                    // Обновляем рамку масштабирования при перемещении
                    PokazatRamuMashtabirovaniya(vybranniyElement);

                    // Если перемещаем объект - обновляем прикрепленные стрелки
                    if (!YavlyaetsyaStrelkoy(vybranniyElement))
                        ObnovitStrelkiDlyaObekta(vybranniyElement);

                    proiskhodiloPeremeshenieElementa = true;
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
                if (proiskhodiloMashtabirovanieElementa)
                {
                    proiskhodiloMashtabirovanieElementa = false;
                    MarkDocumentDirty();
                }
                else
                {
                    proiskhodiloMashtabirovanieElementa = false;
                }
                return;
            }

            if (peremeshayuHolst)
            {
                peremeshayuHolst = false;
                Mouse.Capture(null);
                PoleDlyaRisovaniya.Cursor = Cursors.Arrow;
            }

            // Обрабатываем прикрепление точки изгиба, если мышь была отпущена на холсте
            if (peremeshayuTochkuIzgiba)
            {
                if (tekushayaLiniyaDlyaIzgiba != null && aktivnayaTochkaIzgiba >= 0)
                {
                    var points = tekushayaLiniyaDlyaIzgiba.Points;
                    if (aktivnayaTochkaIzgiba < points.Count)
                    {
                        var isPervayaTochka = aktivnayaTochkaIzgiba == 0;
                        var isPoslednyayaTochka = aktivnayaTochkaIzgiba == points.Count - 1;

                        UIElement objToAttach = null;
                        if (isPervayaTochka || isPoslednyayaTochka)
                        {
                            var mousePos = e.GetPosition(HolstSoderzhanie);
                            objToAttach = NaytiObektVDiapazone(mousePos, RadiusPrikrepleniya);
                        }

                        if (objToAttach != null)
                        {
                            // Используем позицию мыши для точной привязки
                            var mousePos = e.GetPosition(HolstSoderzhanie);
                            PrivyazatTochkuKObektu(tekushayaLiniyaDlyaIzgiba, aktivnayaTochkaIzgiba, objToAttach, mousePos);
                        }
                        else
                        {
                            OtdelitTochkuOtObekta(tekushayaLiniyaDlyaIzgiba, aktivnayaTochkaIzgiba);
                        }
                    }
                }

                peremeshayuTochkuIzgiba = false;
                aktivnayaTochkaIzgiba = -1;
                SkrytPodsvetku();
                Mouse.Capture(null);
                MarkDocumentDirty();
            }

            if (peremeshayuElement)
            {
                peremeshayuElement = false;
                Mouse.Capture(null);

                if (vybranniyElement != null)
                {
                    PokazatRamuMashtabirovaniya(vybranniyElement);
                    // Скрываем подсветку, если не перемещали стрелку
                    if (!YavlyaetsyaStrelkoy(vybranniyElement))
                        SkrytPodsvetku();
                }
            }

            if (proiskhodiloPeremeshenieElementa)
            {
                proiskhodiloPeremeshenieElementa = false;
                MarkDocumentDirty();
            }
            else
            {
                proiskhodiloPeremeshenieElementa = false;
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
            var tochkaSbrosa = e.GetPosition(HolstSoderzhanie);

            UIElement element = SozdatElementPoInstrumentu(instrument, tochkaSbrosa);
            if (element != null)
            {
                bool nachatRedaktirovanieTeksta = string.Equals(instrument, "tekst", StringComparison.OrdinalIgnoreCase) && element is TextBlock;
                DobavitNaHolst(element, true, nachatRedaktirovanieTeksta);
            }
        }

        private void Otmena_Click(object sender, RoutedEventArgs e)
        {
            if (HolstSoderzhanie == null || HolstSoderzhanie.Children.Count == 0) return;

            // Ищем последний реальный элемент (не рамку и не маркеры)
            UIElement element = null;
            for (int i = HolstSoderzhanie.Children.Count - 1; i >= 0; i--)
            {
                var child = HolstSoderzhanie.Children[i] as UIElement;
                if (child == null) continue;

                // Пропускаем рамку и маркеры
                if (ramkaVydeleniya != null && ReferenceEquals(child, ramkaVydeleniya)) continue;
                if (markeriMashtaba != null && child is Border marker && markeriMashtaba.Contains(marker)) continue;
                if (aktivnyTextovyEditor != null && ReferenceEquals(child, aktivnyTextovyEditor)) continue;

                element = child;
                break;
            }

            if (element == null) return;

            // Если удаляем выбранный элемент, скрываем рамку и маркеры
            if (vybranniyElement == element || (vybranniyElement == null && vybranniyeElementy.Contains(element)))
            {
                SkrytRamuMashtabirovaniya();
                SnytVydelenie();
            }

            if (redaktiruemyTextovyElement != null && ReferenceEquals(element, redaktiruemyTextovyElement))
            {
                ZavershitRedaktirovanieTeksta(false);
            }

            HolstSoderzhanie.Children.Remove(element);
            otmenaStack.Push(element);
            vozvratStack.Clear();
            MarkDocumentDirty();
        }

        private void Vozvrat_Click(object sender, RoutedEventArgs e)
        {
            if (otmenaStack.Count == 0) return;
            var element = otmenaStack.Pop();
            HolstSoderzhanie.Children.Add(element);
            vozvratStack.Push(element);

            // Если это был выбранный элемент, обновляем рамку и маркеры
            if (vybranniyElement == element || (vybranniyElement == null && vybranniyeElementy.Contains(element)))
            {
                vybranniyElement = element;
                PokazatRamuMashtabirovaniya(element);
            }
            MarkDocumentDirty();
        }

        private void DobavitNaHolst(UIElement element, bool otslezhivatIzmeneniya = true, bool nachatRedaktirovanieTeksta = false)
        {
            if (HolstSoderzhanie == null || element == null) return;

            HolstSoderzhanie.Children.Add(element);
            vozvratStack.Clear();

            if (element is TextBlock textBlock)
            {
                NastroitTekstovyElement(textBlock, nachatRedaktirovanieTeksta);
            }

            // Сохраняем оригинальные размеры при добавлении элемента
            // Сохраняем координаты для Line элементов сразу
            if (element is Line line)
            {
                if (!originalnyeKoordinatyLinij.ContainsKey(line))
                {
                    originalnyeKoordinatyLinij[line] = new LineCoordinates(line.X1, line.Y1, line.X2, line.Y2);
                }
            }

            // Немного задержки, чтобы элемент успел отрендериться
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var bounds = PoluchitGranitsyBezMashtaba(element);
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    originalnyeRazmery[element] = bounds;
                }

                // Автоматическое прикрепление отключено - пользователь сам выбирает объекты
            }), System.Windows.Threading.DispatcherPriority.Loaded);

            if (otslezhivatIzmeneniya)
            {
                MarkDocumentDirty();
            }
        }

        private UIElement NaytiElementNaHolste(UIElement element)
        {
            var tekushiy = element;
            while (tekushiy != null && tekushiy != HolstSoderzhanie)
            {
                var roditel = VisualTreeHelper.GetParent(tekushiy) as UIElement;
                if (roditel == HolstSoderzhanie)
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

            // Голова актора - черная
            var golova = new Ellipse { Width = 30, Height = 30, Stroke = Brushes.Black, StrokeThickness = standartnayaTolschinaLinii, Fill = Brushes.Black };
            Canvas.SetLeft(golova, 50);
            Canvas.SetTop(golova, 30);

            // Тело, руки и ноги актора - черные
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
                        var liniya = new Polyline
                        {
                            Points = new PointCollection { new Point(tochka.X, tochka.Y), new Point(tochka.X + 120, tochka.Y + 60) },
                            Stroke = Brushes.Black,
                            //StrokeThickness = tekushayaTolschinaLinii
                            StrokeThickness = standartnayaTolschinaLinii
                        };
                        return liniya;
                    }
                case "vklyuchit":
                    {
                        var gruppa = new Canvas();
                        var liniya = new Polyline
                        {
                            Points = new PointCollection { new Point(0, 20), new Point(130, 20) },
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
                        var liniya = new Polyline
                        {
                            Points = new PointCollection { new Point(0, 20), new Point(130, 20) },
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
                        var gruppa = new Canvas();
                        var liniya = new Polyline
                        {
                            Points = new PointCollection { new Point(0, 20), new Point(100, 20) },
                            Stroke = Brushes.Black,
                            StrokeThickness = standartnayaTolschinaLinii
                        };
                        var strelka = new System.Windows.Shapes.Polygon
                        {
                            Points = new PointCollection { new Point(100, 20), new Point(100, 10), new Point(120, 20), new Point(100, 30) },
                            Fill = Brushes.White,
                            Stroke = Brushes.Black,
                            StrokeThickness = standartnayaTolschinaLinii
                        };
                        gruppa.Children.Add(liniya);
                        gruppa.Children.Add(strelka);
                        Canvas.SetLeft(gruppa, tochka.X - 60);
                        Canvas.SetTop(gruppa, tochka.Y - 20);
                        return gruppa;
                    }
                case "tekst":
                    {
                        var blokTeksta = new TextBlock
                        {
                            Text = "Текст",
                            FontSize = 16,
                            Foreground = Brushes.Black,
                            Tag = TagPolzovatelskogoTeksta
                        };
                        Canvas.SetLeft(blokTeksta, tochka.X - 20);
                        Canvas.SetTop(blokTeksta, tochka.Y - 10);
                        return blokTeksta;
                    }
                default:
                    return null;
            }
        }

        private bool EtoPolzovatelskiyTekst(TextBlock textBlock)
        {
            if (textBlock == null)
            {
                return false;
            }

            var tag = textBlock.Tag as string;
            if (!string.IsNullOrEmpty(tag) && tag == TagPolzovatelskogoTeksta)
            {
                return true;
            }

            if (HolstSoderzhanie == null)
            {
                return false;
            }

            var parent = VisualTreeHelper.GetParent(textBlock);
            return ReferenceEquals(parent, HolstSoderzhanie);
        }

        private void NastroitTekstovyElement(TextBlock textBlock, bool nachatRedaktirovanieSrazu = false)
        {
            if (textBlock == null || HolstSoderzhanie == null)
            {
                return;
            }

            if (!EtoPolzovatelskiyTekst(textBlock))
            {
                return;
            }

            textBlock.Cursor = Cursors.IBeam;
            textBlock.MouseLeftButtonDown -= TekstovyElement_MouseLeftButtonDown;
            textBlock.MouseLeftButtonDown += TekstovyElement_MouseLeftButtonDown;
            PrimeniKompensiruyushchiyMashtab(textBlock, PoluchitFaktorNezavisimostiOtMashtaba());

            if (nachatRedaktirovanieSrazu)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    NachatRedaktirovanieTeksta(textBlock, true);
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        private double PoluchitFaktorNezavisimostiOtMashtaba()
        {
            return tekushiyMashtab <= 0 ? 1.0 : 1.0 / tekushiyMashtab;
        }

        private void PrimeniKompensiruyushchiyMashtab(FrameworkElement element, double faktor)
        {
            if (element == null)
            {
                return;
            }

            if (element.LayoutTransform is ScaleTransform scale)
            {
                scale.ScaleX = faktor;
                scale.ScaleY = faktor;
            }
            else
            {
                element.LayoutTransform = new ScaleTransform(faktor, faktor);
            }
        }

        private void ObnovitMashtabTeksta()
        {
            var faktor = PoluchitFaktorNezavisimostiOtMashtaba();

            if (HolstSoderzhanie != null)
            {
                foreach (var textBlock in HolstSoderzhanie.Children.OfType<TextBlock>())
                {
                    if (EtoPolzovatelskiyTekst(textBlock))
                    {
                        PrimeniKompensiruyushchiyMashtab(textBlock, faktor);
                    }
                }
            }

            if (aktivnyTextovyEditor != null)
            {
                PrimeniKompensiruyushchiyMashtab(aktivnyTextovyEditor, faktor);
            }
        }

        private void VstavitPerehodNaNovuyuStroku(TextBox editor)
        {
            if (editor == null)
            {
                return;
            }

            var start = editor.SelectionStart;
            editor.SelectedText = Environment.NewLine;
            editor.CaretIndex = start + Environment.NewLine.Length;
        }

        private bool IstochnikVnutriAktivnogoRedaktora(DependencyObject source)
        {
            if (aktivnyTextovyEditor == null || source == null)
            {
                return false;
            }

            var current = source;
            while (current != null)
            {
                if (ReferenceEquals(current, aktivnyTextovyEditor))
                {
                    return true;
                }
                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void TekstovyElement_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount < 2)
            {
                return;
            }

            if (sender is TextBlock textBlock && EtoPolzovatelskiyTekst(textBlock))
            {
                e.Handled = true;
                NachatRedaktirovanieTeksta(textBlock, false);
            }
        }

        private void NachatRedaktirovanieTeksta(TextBlock textBlock, bool vybratVse)
        {
            if (textBlock == null || HolstSoderzhanie == null)
            {
                return;
            }

            if (!EtoPolzovatelskiyTekst(textBlock))
            {
                return;
            }

            if (aktivnyTextovyEditor != null)
            {
                if (ReferenceEquals(redaktiruemyTextovyElement, textBlock))
                {
                    return;
                }
                ZavershitRedaktirovanieTeksta(true);
            }

            redaktiruemyTextovyElement = textBlock;

            double left = Canvas.GetLeft(textBlock);
            double top = Canvas.GetTop(textBlock);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var desired = textBlock.DesiredSize;
            double minWidth = Math.Max(80, desired.Width + 12);
            double minHeight = Math.Max(textBlock.FontSize + 8, desired.Height + 8);

            var editor = new TextBox
            {
                Text = textBlock.Text ?? string.Empty,
                FontSize = textBlock.FontSize > 0 ? textBlock.FontSize : 16,
                FontFamily = textBlock.FontFamily,
                FontWeight = textBlock.FontWeight,
                Foreground = textBlock.Foreground ?? Brushes.Black,
                Background = new SolidColorBrush(Color.FromArgb(235, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(205, 133, 63)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 4, 8, 4),
                MinWidth = minWidth,
                MinHeight = minHeight,
                AcceptsReturn = false,
                TextWrapping = TextWrapping.Wrap
            };
            editor.HorizontalContentAlignment = HorizontalAlignment.Left;
            editor.VerticalContentAlignment = VerticalAlignment.Top;

            aktivnyTextovyEditor = editor;

            Canvas.SetLeft(editor, left);
            Canvas.SetTop(editor, top);
            Panel.SetZIndex(editor, Panel.GetZIndex(textBlock) + 1);

            textBlock.Visibility = Visibility.Collapsed;

            HolstSoderzhanie.Children.Add(editor);
            PrimeniKompensiruyushchiyMashtab(editor, PoluchitFaktorNezavisimostiOtMashtaba());

            editor.LostKeyboardFocus += TextEditor_LostKeyboardFocus;
            editor.KeyDown += TextEditor_KeyDown;

            RoutedEventHandler loadedHandler = null;
            loadedHandler = (s, _) =>
            {
                editor.Loaded -= loadedHandler;
                editor.Focus();
                if (vybratVse)
                {
                    editor.SelectAll();
                }
                else
                {
                    editor.CaretIndex = editor.Text.Length;
                }
            };
            editor.Loaded += loadedHandler;
        }

        private void TextEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    if (sender is TextBox textBox)
                    {
                        VstavitPerehodNaNovuyuStroku(textBox);
                    }
                    e.Handled = true;
                    return;
                }

                ZavershitRedaktirovanieTeksta(true);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                ZavershitRedaktirovanieTeksta(false);
                e.Handled = true;
            }
        }

        private void TextEditor_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            ZavershitRedaktirovanieTeksta(true);
        }

        private void ZavershitRedaktirovanieTeksta(bool sohranitIzmeneniya)
        {
            if (aktivnyTextovyEditor == null)
            {
                redaktiruemyTextovyElement = null;
                return;
            }

            var editor = aktivnyTextovyEditor;
            var textBlock = redaktiruemyTextovyElement;

            editor.LostKeyboardFocus -= TextEditor_LostKeyboardFocus;
            editor.KeyDown -= TextEditor_KeyDown;

            HolstSoderzhanie?.Children.Remove(editor);

            aktivnyTextovyEditor = null;
            redaktiruemyTextovyElement = null;

            if (textBlock == null)
            {
                return;
            }

            if (sohranitIzmeneniya)
            {
                var novyyTekst = editor.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(novyyTekst))
                {
                    novyyTekst = "Текст";
                }

                if (!string.Equals(textBlock.Text, novyyTekst, StringComparison.Ordinal))
                {
                    textBlock.Text = novyyTekst;
                    MarkDocumentDirty();
                }
            }

            textBlock.Visibility = Visibility.Visible;
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
            if (!ProveritNuzhnoLiSohranitPeredDeystviem())
            {
                return;
            }

            SozdatNovyyDokument();
        }

        private void OpenFile_Click(object sender, RoutedEventArgs e)
        {
            if (!ProveritNuzhnoLiSohranitPeredDeystviem())
            {
                return;
            }

            var dialog = new OpenFileDialog
            {
                Filter = "Use Case App (*.uca)|*.uca|Все файлы (*.*)|*.*",
                DefaultExt = "uca",
                Multiselect = false,
                Title = "Открыть диаграмму"
            };

            if (dialog.ShowDialog() == true)
            {
                ZagruzitDiagrammuIzFaila(dialog.FileName);
            }
        }

        private void SaveFile_Click(object sender, RoutedEventArgs e)
        {
            SohranitDiagrammu();
        }

        private void SaveAsFile_Click(object sender, RoutedEventArgs e)
        {
            SohranitDiagrammu(true);
        }

        private bool SohranitDiagrammu(bool prinuditelnoyeVyborMesta = false)
        {
            if (HolstSoderzhanie == null)
            {
                return false;
            }

            string targetPath = tekushiyPutFayla;
            if (prinuditelnoyeVyborMesta || string.IsNullOrWhiteSpace(targetPath))
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Use Case App (*.uca)|*.uca|Все файлы (*.*)|*.*",
                    DefaultExt = "uca",
                    AddExtension = true,
                    Title = "Сохранить диаграмму",
                    FileName = string.IsNullOrWhiteSpace(tekushiyPutFayla) ? "Диаграмма" : System.IO.Path.GetFileName(tekushiyPutFayla)
                };

                if (dialog.ShowDialog() == true)
                {
                    targetPath = dialog.FileName;
                }
                else
                {
                    return false;
                }
            }

            if (!EksportDiagrammy(targetPath))
            {
                return false;
            }

            tekushiyPutFayla = targetPath;
            MarkDocumentClean();
            return true;
        }

        private bool EksportDiagrammy(string filePath)
        {
            try
            {
                var snapshot = PostroitSnimokDiagrammy();
                var serializer = new DataContractJsonSerializer(typeof(DiagramFile));
                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    serializer.WriteObject(stream, snapshot);
                }

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось сохранить файл.\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private DiagramFile PostroitSnimokDiagrammy()
        {
            var diagram = new DiagramFile
            {
                Zoom = PolzunokMashtaba?.Value ?? 100,
                OffsetX = TransformSdviga?.X ?? 0,
                OffsetY = TransformSdviga?.Y ?? 0,
                IsGridVisible = FonSetki?.Visibility != Visibility.Hidden
            };

            if (HolstSoderzhanie == null) return diagram;
            foreach (UIElement child in HolstSoderzhanie.Children)
            {
                if (child == null) continue;
                if (ramkaVydeleniya != null && ReferenceEquals(child, ramkaVydeleniya)) continue;
                if (markeriMashtaba != null && child is Border marker && markeriMashtaba.Contains(marker)) continue;
                if (aktivnyTextovyEditor != null && ReferenceEquals(child, aktivnyTextovyEditor)) continue;

                try
                {
                    string elementXaml = XamlWriter.Save(child);
                    var elementData = new DiagramElement
                    {
                        Xaml = elementXaml,
                        Left = double.IsNaN(Canvas.GetLeft(child)) ? (double?)null : Canvas.GetLeft(child),
                        Top = double.IsNaN(Canvas.GetTop(child)) ? (double?)null : Canvas.GetTop(child),
                        ZIndex = Panel.GetZIndex(child)
                    };
                    diagram.Elements.Add(elementData);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Не удалось сериализовать элемент.\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            return diagram;
        }

        private bool ZagruzitDiagrammuIzFaila(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return false;
            }

            try
            {
                DiagramFile diagram;
                var serializer = new DataContractJsonSerializer(typeof(DiagramFile));
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    diagram = serializer.ReadObject(stream) as DiagramFile;
                }

                if (diagram == null)
                {
                    throw new InvalidOperationException("Файл повреждён или имеет неподдерживаемый формат.");
                }

                blokirovatOtslezhivanieIzmeneniy = true;
                try
                {
                    OchistitHolstCore();
                    PriminitDiagrammu(diagram);
                }
                finally
                {
                    blokirovatOtslezhivanieIzmeneniy = false;
                }

                tekushiyPutFayla = filePath;
                MarkDocumentClean();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Не удалось открыть файл.\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void PriminitDiagrammu(DiagramFile diagram)
        {
            if (diagram == null || HolstSoderzhanie == null)
            {
                return;
            }

            if (diagram.Elements != null)
            {
                foreach (var elementData in diagram.Elements)
                {
                    if (elementData == null || string.IsNullOrWhiteSpace(elementData.Xaml))
                    {
                        continue;
                    }

                    UIElement element;
                    try
                    {
                        element = XamlReader.Parse(elementData.Xaml) as UIElement;
                    }
                    catch
                    {
                        continue;
                    }

                    if (element == null)
                    {
                        continue;
                    }

                    DobavitNaHolst(element, false);

                    if (elementData.Left.HasValue)
                    {
                        Canvas.SetLeft(element, elementData.Left.Value);
                    }
                    else
                    {
                        Canvas.SetLeft(element, double.NaN);
                    }

                    if (elementData.Top.HasValue)
                    {
                        Canvas.SetTop(element, elementData.Top.Value);
                    }
                    else
                    {
                        Canvas.SetTop(element, double.NaN);
                    }

                    Panel.SetZIndex(element, elementData.ZIndex);
                }
            }

            if (PolzunokMashtaba != null && diagram.Zoom > 0)
            {
                var zoomValue = Math.Max(PolzunokMashtaba.Minimum, Math.Min(diagram.Zoom, PolzunokMashtaba.Maximum));
                PolzunokMashtaba.Value = zoomValue;
            }

            if (TransformSdviga != null)
            {
                TransformSdviga.X = diagram.OffsetX;
                TransformSdviga.Y = diagram.OffsetY;
            }
            if (setkaTranslateTransform != null)
            {
                setkaTranslateTransform.X = diagram.OffsetX;
                setkaTranslateTransform.Y = diagram.OffsetY;
            }

            if (PerekyuchatelSetki != null)
            {
                PerekyuchatelSetki.IsChecked = diagram.IsGridVisible;
            }
            else if (FonSetki != null)
            {
                FonSetki.Visibility = diagram.IsGridVisible ? Visibility.Visible : Visibility.Hidden;
            }
        }

        private void OchistitHolstCore()
        {
            ZavershitRedaktirovanieTeksta(false);
            SnytVydelenie();
            SkrytRamuMashtabirovaniya();
            HolstSoderzhanie?.Children.Clear();
            otmenaStack.Clear();
            vozvratStack.Clear();
            originalnyeRazmery.Clear();
            originalnyeTolschiny.Clear();
            originalnyeKoordinatyLinij.Clear();
            vybranniyElement = null;
            vybranniyeElementy.Clear();
            proiskhodiloPeremeshenieElementa = false;
            proiskhodiloMashtabirovanieElementa = false;
        }

        private void SozdatNovyyDokument()
        {
            blokirovatOtslezhivanieIzmeneniy = true;
            try
            {
                OchistitHolstCore();

                if (TransformSdviga != null)
                {
                    TransformSdviga.X = 0;
                    TransformSdviga.Y = 0;
                }
                if (setkaTranslateTransform != null)
                {
                    setkaTranslateTransform.X = 0;
                    setkaTranslateTransform.Y = 0;
                }

                if (TransformMashtaba != null)
                {
                    TransformMashtaba.ScaleX = 1;
                    TransformMashtaba.ScaleY = 1;
                }
                if (setkaScaleTransform != null)
                {
                    setkaScaleTransform.ScaleX = 1;
                    setkaScaleTransform.ScaleY = 1;
                }

                if (PolzunokMashtaba != null)
                {
                    PolzunokMashtaba.Value = 100;
                }
                tekushiyMashtab = 1.0;
                ObnovitMashtabTeksta();

                if (PerekyuchatelSetki != null)
                {
                    PerekyuchatelSetki.IsChecked = true;
                }
                else if (FonSetki != null)
                {
                    FonSetki.Visibility = Visibility.Visible;
                }

                if (TekstTolschiny != null)
                {
                    TekstTolschiny.Text = tekushayaTolschinaLinii.ToString();
                }
            }
            finally
            {
                blokirovatOtslezhivanieIzmeneniy = false;
            }

            tekushiyPutFayla = null;
            MarkDocumentClean();
        }

        private bool ProveritNuzhnoLiSohranitPeredDeystviem()
        {
            ZavershitRedaktirovanieTeksta(true);

            if (!estNesokhrannyeIzmeneniya)
            {
                return true;
            }

            var result = MessageBox.Show("Диаграмма содержит несохранённые изменения. Сохранить изменения?", "Сохранить изменения", MessageBoxButton.YesNoCancel, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Cancel)
            {
                return false;
            }

            if (result == MessageBoxResult.Yes)
            {
                return SohranitDiagrammu();
            }

            return true;
        }

        private void MarkDocumentDirty()
        {
            if (blokirovatOtslezhivanieIzmeneniy)
            {
                return;
            }

            estNesokhrannyeIzmeneniya = true;
            ObnovitZagolovokOkna();
        }

        private void MarkDocumentClean()
        {
            estNesokhrannyeIzmeneniya = false;
            ObnovitZagolovokOkna();
        }

        private void ObnovitZagolovokOkna()
        {
            var fileName = string.IsNullOrWhiteSpace(tekushiyPutFayla) ? "Безымянный" : System.IO.Path.GetFileName(tekushiyPutFayla);
            Title = estNesokhrannyeIzmeneniya ? $"UCA - {fileName}*" : $"UCA - {fileName}";
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            if (!ProveritNuzhnoLiSohranitPeredDeystviem())
            {
                e.Cancel = true;
            }
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
            NewFile_Click(sender, e);
        }

        private void Otkryt_Click(object sender, RoutedEventArgs e)
        {
            OpenFile_Click(sender, e);
        }

        private void Sohranit_Click(object sender, RoutedEventArgs e)
        {
            SaveFile_Click(sender, e);
        }

        private void SohranitKak_Click(object sender, RoutedEventArgs e)
        {
            SaveAsFile_Click(sender, e);
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

        [DataContract]
        private class DiagramFile
        {
            [DataMember] public List<DiagramElement> Elements { get; set; } = new List<DiagramElement>();
            [DataMember] public double Zoom { get; set; } = 100;
            [DataMember] public double OffsetX { get; set; }
            [DataMember] public double OffsetY { get; set; }
            [DataMember] public bool IsGridVisible { get; set; } = true;
        }

        [DataContract]
        private class DiagramElement
        {
            [DataMember] public string Xaml { get; set; }
            [DataMember] public double? Left { get; set; }
            [DataMember] public double? Top { get; set; }
            [DataMember] public int ZIndex { get; set; }
        }

        private class LineCoordinates
        {
            public LineCoordinates(double x1, double y1, double x2, double y2)
            {
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
            }

            public double X1 { get; }
            public double Y1 { get; }
            public double X2 { get; }
            public double Y2 { get; }
        }


        private bool YavlyaetsyaStrelkoy(UIElement element)
        {
            return element is Polyline || (element is Canvas c && c.Children.OfType<Polyline>().Any());
        }

        private void ObnovitStrelkiDlyaObekta(UIElement obj)
        {
            if (obj == null) return;
            foreach (var kvp in prikreplennyeStrelki.ToList())
            {
                if (kvp.Value.Item1 == obj || kvp.Value.Item2 == obj)
                    PrivyazatStrelku(kvp.Key);
            }
        }

        private void PrivyazatStrelku(UIElement strelka)
        {
            if (strelka == null || HolstSoderzhanie == null) return;

            Polyline polyline = null;
            Canvas canvas = null;
            double canvasLeft = 0, canvasTop = 0;

            if (strelka is Canvas c)
            {
                canvas = c;
                polyline = canvas.Children.OfType<Polyline>().FirstOrDefault();
                if (polyline?.Points == null || polyline.Points.Count < 2) return;
                canvasLeft = Canvas.GetLeft(canvas); if (double.IsNaN(canvasLeft)) canvasLeft = 0;
                canvasTop = Canvas.GetTop(canvas); if (double.IsNaN(canvasTop)) canvasTop = 0;
            }
            else if (strelka is Polyline pl)
            {
                if (pl.Points == null || pl.Points.Count < 2) return;
                polyline = pl;
            }
            else return;

            if (!prikreplennyeStrelki.ContainsKey(strelka)) return;
            var savedAttachments = prikreplennyeStrelki[strelka];
            var obj1 = savedAttachments.Item1;
            var obj2 = savedAttachments.Item2;

            if (obj1 == null && obj2 == null) return;

            bool updated = false;
            Point absP1 = polyline.Points[0];
            Point absP2 = polyline.Points[polyline.Points.Count - 1];
            if (canvas != null)
            {
                absP1 = new Point(absP1.X + canvasLeft, absP1.Y + canvasTop);
                absP2 = new Point(absP2.X + canvasLeft, absP2.Y + canvasTop);
            }

            if (obj1 != null)
            {
<<<<<<< Updated upstream
                // Всегда используем направление от центра объекта к другой точке линии
                // Направляемся на вторую точку линии (absP2), чтобы конец линии был направлен на неё
                Point target = absP2;
                // Используем NaytiTochkuNaGranitseOtTsentra для правильного направления на другую точку
                var t = NaytiTochkuNaGranitseOtTsentra(target, obj1);
=======
                // Используем сохраненную относительную позицию на контуре объекта
                var key1 = new Tuple<UIElement, int>(strelka, 0);
                Point t;
                if (YavlyaetsyaAktorom(obj1))
                {
                    var bounds = PoluchitGranitsyElementa(obj1);
                    t = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                }
                else if (tochkiPrikrepleniya.ContainsKey(key1) && tochkiPrikrepleniya[key1].Item1 == obj1)
                {
                    // Восстанавливаем точку по сохраненной относительной позиции
                    var otnositelnayaPozitsiya = tochkiPrikrepleniya[key1].Item2;
                    t = VosstanovitTochkuNaKonture(otnositelnayaPozitsiya, obj1);
                }
                else
                {
                    // Если сохраненной позиции нет, используем центр obj2 как target
                    Point target;
                    if (obj2 != null)
                    {
                        var bounds2 = PoluchitGranitsyElementa(obj2);
                        target = new Point(bounds2.X + bounds2.Width / 2, bounds2.Y + bounds2.Height / 2);
                    }
                    else
                    {
                        target = absP2;
                    }
                    t = NaytiTochkuNaGranitseOtTsentra(target, obj1);
                }
>>>>>>> Stashed changes
                if (canvas != null)
                    polyline.Points[0] = new Point(t.X - canvasLeft, t.Y - canvasTop);
                else
                    polyline.Points[0] = t;
                updated = true;
            }

            if (obj2 != null)
            {
<<<<<<< Updated upstream
                // Всегда используем направление от центра объекта к другой точке линии
                // Направляемся на первую точку линии (absP1), чтобы конец линии был направлен на неё
                Point target = absP1;
                // Используем NaytiTochkuNaGranitseOtTsentra для правильного направления на другую точку
                var t = NaytiTochkuNaGranitseOtTsentra(target, obj2);
                var idx = polyline.Points.Count - 1;
=======
                // Используем сохраненную относительную позицию на контуре объекта
                var idx = polyline.Points.Count - 1;
                var key2 = new Tuple<UIElement, int>(strelka, idx);
                Point t;
                if (YavlyaetsyaAktorom(obj2))
                {
                    var bounds = PoluchitGranitsyElementa(obj2);
                    t = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
                }
                else if (tochkiPrikrepleniya.ContainsKey(key2) && tochkiPrikrepleniya[key2].Item1 == obj2)
                {
                    // Восстанавливаем точку по сохраненной относительной позиции
                    var otnositelnayaPozitsiya = tochkiPrikrepleniya[key2].Item2;
                    t = VosstanovitTochkuNaKonture(otnositelnayaPozitsiya, obj2);
                }
                else
                {
                    // Если сохраненной позиции нет, используем центр obj1 как target
                    Point target;
                    if (obj1 != null)
                    {
                        var bounds1 = PoluchitGranitsyElementa(obj1);
                        target = new Point(bounds1.X + bounds1.Width / 2, bounds1.Y + bounds1.Height / 2);
                    }
                    else
                    {
                        target = absP1;
                    }
                    t = NaytiTochkuNaGranitseOtTsentra(target, obj2);
                }
>>>>>>> Stashed changes
                if (canvas != null)
                    polyline.Points[idx] = new Point(t.X - canvasLeft, t.Y - canvasTop);
                else
                    polyline.Points[idx] = t;
                updated = true;
            }

            if (updated && canvas != null)
                ObnovitStrelku(canvas, polyline);

            if (markeriIzgiba != null && polyline != null)
            {
                var points = polyline.Points;
                for (int i = 0; i < points.Count && i < markeriIzgiba.Count; i++)
                {
                    var marker = markeriIzgiba[i];
                    Point absPoint;
                    if (canvas != null)
                        absPoint = new Point(points[i].X + canvasLeft, points[i].Y + canvasTop);
                    else
                        absPoint = points[i];
                    Canvas.SetLeft(marker, absPoint.X - marker.Width / 2);
                    Canvas.SetTop(marker, absPoint.Y - marker.Height / 2);
                }
            }

            prikreplennyeStrelki[strelka] = new Tuple<UIElement, UIElement>(obj1, obj2);
        }

        private UIElement NaytiBlizhayshiyObekt(Point p)
        {
            if (HolstSoderzhanie == null) return null;
            UIElement nearest = null;
            double minDist = 150;

            foreach (UIElement el in HolstSoderzhanie.Children)
            {
                if (el == null) continue;
                if (YavlyaetsyaStrelkoy(el)) continue;
                if (el == ramkaVydeleniya) continue;
                if (markeriMashtaba != null && markeriMashtaba.Contains(el)) continue;
                if (markeriIzgiba != null && markeriIzgiba.Contains(el)) continue;
                if (podsvetkiObektov != null && podsvetkiObektov.Contains(el)) continue;
                if (aktivnyTextovyEditor != null && ReferenceEquals(el, aktivnyTextovyEditor)) continue;

                var bounds = PoluchitGranitsyBezMashtaba(el);
                if (bounds.Width <= 0 || bounds.Height <= 0) continue;

                var expandedBounds = new Rect(bounds.Left - 10, bounds.Top - 10, bounds.Width + 20, bounds.Height + 20);
                
                var nearestPoint = new Point(
                    Math.Max(bounds.Left, Math.Min(p.X, bounds.Right)),
                    Math.Max(bounds.Top, Math.Min(p.Y, bounds.Bottom))
                );
                
                if (p.X >= bounds.Left && p.X <= bounds.Right && p.Y >= bounds.Top && p.Y <= bounds.Bottom)
                {
                    var dLeft = p.X - bounds.Left;
                    var dRight = bounds.Right - p.X;
                    var dTop = p.Y - bounds.Top;
                    var dBottom = bounds.Bottom - p.Y;
                    var min = Math.Min(Math.Min(dLeft, dRight), Math.Min(dTop, dBottom));
                    
                    if (min == dLeft) nearestPoint = new Point(bounds.Left, p.Y);
                    else if (min == dRight) nearestPoint = new Point(bounds.Right, p.Y);
                    else if (min == dTop) nearestPoint = new Point(p.X, bounds.Top);
                    else nearestPoint = new Point(p.X, bounds.Bottom);
                }
                
                var dist = Math.Sqrt(Math.Pow(p.X - nearestPoint.X, 2) + Math.Pow(p.Y - nearestPoint.Y, 2));
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = el;
                }
            }
            return nearest;
        }

        private UIElement NaytiObektVDiapazone(Point p, double radius)
        {
            if (HolstSoderzhanie == null) return null;
            UIElement nearest = null;
            double minDist = double.MaxValue;

            foreach (UIElement el in HolstSoderzhanie.Children)
            {
                if (el == null) continue;
                if (YavlyaetsyaStrelkoy(el)) continue;
                if (el == ramkaVydeleniya) continue;
                if (markeriMashtaba != null && markeriMashtaba.Contains(el)) continue;
                if (markeriIzgiba != null && markeriIzgiba.Contains(el)) continue;
                if (podsvetkiObektov != null && podsvetkiObektov.Contains(el)) continue;
                if (aktivnyTextovyEditor != null && ReferenceEquals(el, aktivnyTextovyEditor)) continue;

                var bounds = PoluchitGranitsyBezMashtaba(el);
                if (bounds.Width <= 0 || bounds.Height <= 0) continue;

                // Вычисляем динамический радиус на основе размера объекта
                // Радиус должен быть достаточным, чтобы полностью охватить объект
                var objectDiagonal = Math.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height);
                var dynamicRadius = Math.Max(radius, objectDiagonal / 2 + 50); // Минимум базовый радиус, но не меньше диагонали/2 + запас

                // Расширяем границы объекта на динамический радиус
                var expanded = new Rect(
                    bounds.Left - dynamicRadius,
                    bounds.Top - dynamicRadius,
                    bounds.Width + dynamicRadius * 2,
                    bounds.Height + dynamicRadius * 2
                );

                // Проверяем, попадает ли точка в расширенную область
                if (expanded.Contains(p))
                {
                    // Находим ближайшую точку на краю объекта для определения расстояния
                    var nearestPoint = new Point(
                        Math.Max(bounds.Left, Math.Min(p.X, bounds.Right)),
                        Math.Max(bounds.Top, Math.Min(p.Y, bounds.Bottom))
                    );

                    // Если точка внутри объекта, находим ближайший край
                    if (p.X >= bounds.Left && p.X <= bounds.Right && p.Y >= bounds.Top && p.Y <= bounds.Bottom)
                    {
                        var dLeft = p.X - bounds.Left;
                        var dRight = bounds.Right - p.X;
                        var dTop = p.Y - bounds.Top;
                        var dBottom = bounds.Bottom - p.Y;
                        var min = Math.Min(Math.Min(dLeft, dRight), Math.Min(dTop, dBottom));

                        if (min == dLeft) nearestPoint = new Point(bounds.Left, p.Y);
                        else if (min == dRight) nearestPoint = new Point(bounds.Right, p.Y);
                        else if (min == dTop) nearestPoint = new Point(p.X, bounds.Top);
                        else nearestPoint = new Point(p.X, bounds.Bottom);
                    }

                    var dist = Math.Sqrt(Math.Pow(p.X - nearestPoint.X, 2) + Math.Pow(p.Y - nearestPoint.Y, 2));
                    if (dist < minDist)
                    {
                        minDist = dist;
                        nearest = el;
                    }
                }
            }
            return nearest;
        }

        private Point NaytiTochkuNaGranitse(Point p, UIElement obj)
        {
            var bounds = PoluchitGranitsyBezMashtaba(obj);
            var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            
            // Вычисляем вектор от центра к точке
            var dx = p.X - center.X;
            var dy = p.Y - center.Y;
            
            // Если точка очень близко к центру, возвращаем правую сторону
            if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
                return new Point(bounds.Right, center.Y);
            
            // Вычисляем расстояния до каждой стороны по направлению от центра
            var halfWidth = bounds.Width / 2;
            var halfHeight = bounds.Height / 2;
            
            // Находим, какая сторона пересекается первой при движении от центра к точке
            double tX = double.MaxValue;
            double tY = double.MaxValue;
            
            if (Math.Abs(dx) > 0.0001)
            {
                if (dx > 0)
                    tX = halfWidth / dx;
                else
                    tX = -halfWidth / dx;
            }
            
            if (Math.Abs(dy) > 0.0001)
            {
                if (dy > 0)
                    tY = halfHeight / dy;
                else
                    tY = -halfHeight / dy;
            }
            
            // Выбираем ближайшее пересечение (меньшее t означает более близкую сторону)
            var t = Math.Min(tX, tY);
            
            // Вычисляем точку пересечения
            var x = center.X + dx * t;
            var y = center.Y + dy * t;
            
            // Ограничиваем точку границами прямоугольника
            x = Math.Max(bounds.Left, Math.Min(bounds.Right, x));
            y = Math.Max(bounds.Top, Math.Min(bounds.Bottom, y));
            
            // Определяем, на какой стороне находится точка, и привязываем точно к краю
            var epsilon = 0.1; // Небольшое значение для сравнения
            if (Math.Abs(x - bounds.Left) < epsilon)
                return new Point(bounds.Left, y);
            else if (Math.Abs(x - bounds.Right) < epsilon)
                return new Point(bounds.Right, y);
            else if (Math.Abs(y - bounds.Top) < epsilon)
                return new Point(x, bounds.Top);
            else
                return new Point(x, bounds.Bottom);
        }

        private Point NaytiTochkuNaGranitseOtTsentra(Point p, UIElement obj)
        {
            var bounds = PoluchitGranitsyBezMashtaba(obj);
            var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            var dx = p.X - center.X;
            var dy = p.Y - center.Y;
            
            if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
                return new Point(bounds.Right, center.Y);
            
            // Находим точку пересечения луча от центра к точке p с границей прямоугольника
            // Определяем, какая сторона пересекается первой
            var halfWidth = bounds.Width / 2;
            var halfHeight = bounds.Height / 2;
            
            double tX = double.MaxValue;
            if (Math.Abs(dx) > 0.0001)
            {
                if (dx > 0)
                    tX = halfWidth / dx;
                else
                    tX = -halfWidth / dx;
            }
            
            double tY = double.MaxValue;
            if (Math.Abs(dy) > 0.0001)
            {
                if (dy > 0)
                    tY = halfHeight / dy;
                else
                    tY = -halfHeight / dy;
            }
            
            // Выбираем ближайшее пересечение
            var t = Math.Min(tX, tY);
            
            // Определяем, какая сторона пересекается первой на основе значения t
            if (tX < tY)
            {
                // Пересекается вертикальная сторона (левая или правая)
                var x = dx > 0 ? bounds.Right : bounds.Left;
                var y = center.Y + dy * t;
                // Ограничиваем y границами прямоугольника
                y = Math.Max(bounds.Top, Math.Min(bounds.Bottom, y));
                return new Point(x, y);
            }
            else
            {
                // Пересекается горизонтальная сторона (верхняя или нижняя)
                var x = center.X + dx * t;
                var y = dy > 0 ? bounds.Bottom : bounds.Top;
                // Ограничиваем x границами прямоугольника
                x = Math.Max(bounds.Left, Math.Min(bounds.Right, x));
                return new Point(x, y);
            }
        }

        private void ObnovitStrelku(Canvas canvas, Polyline polyline)
        {
            if (polyline.StrokeDashArray == null || polyline.StrokeDashArray.Count == 0)
                ObnovitStrelkuObobsheniya(canvas, polyline.Points);
            else
                ObnovitStrelkuDlyaCanvas(canvas, polyline.Points);
        }
        private void SkrytPodsvetku()
        {
            if (HolstSoderzhanie == null) return;
            foreach (var border in podsvetkiObektov)
            {
                if (HolstSoderzhanie.Children.Contains(border))
                    HolstSoderzhanie.Children.Remove(border);
            }
            podsvetkiObektov.Clear();
        }
        // Вычисляет относительную позицию точки на контуре объекта (для сохранения)
        private Point VychislitOtnositelnuyuPozitsiyuNaKonture(Point mousePos, UIElement obj)
        {
            var bounds = PoluchitGranitsyElementa(obj);
            var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            var dx = mousePos.X - center.X;
            var dy = mousePos.Y - center.Y;
            
            // Для эллипсов сохраняем угол
            if (obj is Ellipse && bounds.Width > 0 && bounds.Height > 0)
            {
                var angle = Math.Atan2(dy, dx);
                return new Point(angle, 0); // X = угол, Y = 0 (не используется)
            }
            
            // Для прямоугольников сохраняем относительную позицию на стороне
            // Определяем, на какой стороне находится точка
            var halfWidth = bounds.Width / 2;
            var halfHeight = bounds.Height / 2;
            
            double tX = double.MaxValue, tY = double.MaxValue;
            if (Math.Abs(dx) > 0.0001)
            {
                if (dx > 0) tX = halfWidth / dx;
                else tX = -halfWidth / dx;
            }
            if (Math.Abs(dy) > 0.0001)
            {
                if (dy > 0) tY = halfHeight / dy;
                else tY = -halfHeight / dy;
            }
            
            var t = Math.Min(tX, tY);
            var x = center.X + dx * t;
            var y = center.Y + dy * t;
            
            // Определяем сторону и сохраняем относительную позицию
            if (tX < tY)
            {
                // Вертикальная сторона (левая или правая)
                var side = dx > 0 ? 1.0 : -1.0; // 1 = правая, -1 = левая
                var relY = (y - bounds.Top) / bounds.Height; // Относительная позиция по высоте (0-1)
                return new Point(side, relY);
            }
            else
            {
                // Горизонтальная сторона (верхняя или нижняя)
                var side = dy > 0 ? 2.0 : -2.0; // 2 = нижняя, -2 = верхняя
                var relX = (x - bounds.Left) / bounds.Width; // Относительная позиция по ширине (0-1)
                return new Point(side, relX);
            }
        }
        
        // Восстанавливает точку на контуре по относительной позиции
        private Point VosstanovitTochkuNaKonture(Point otnositelnayaPozitsiya, UIElement obj)
        {
            var bounds = PoluchitGranitsyElementa(obj);
            var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            
            // Для эллипсов используем угол
            if (obj is Ellipse && bounds.Width > 0 && bounds.Height > 0)
            {
                var angle = otnositelnayaPozitsiya.X;
                var a = bounds.Width / 2;
                var b = bounds.Height / 2;
                var x = center.X + a * Math.Cos(angle);
                var y = center.Y + b * Math.Sin(angle);
                return new Point(x, y);
            }
            
            // Для прямоугольников используем сохраненную сторону и относительную позицию
            var side = otnositelnayaPozitsiya.X;
            var relPos = otnositelnayaPozitsiya.Y;
            
            if (Math.Abs(side - 1.0) < 0.1) // Правая сторона
            {
                var y = bounds.Top + relPos * bounds.Height;
                return new Point(bounds.Right, y);
            }
            else if (Math.Abs(side + 1.0) < 0.1) // Левая сторона
            {
                var y = bounds.Top + relPos * bounds.Height;
                return new Point(bounds.Left, y);
            }
            else if (Math.Abs(side - 2.0) < 0.1) // Нижняя сторона
            {
                var x = bounds.Left + relPos * bounds.Width;
                return new Point(x, bounds.Bottom);
            }
            else // Верхняя сторона
            {
                var x = bounds.Left + relPos * bounds.Width;
                return new Point(x, bounds.Top);
            }
        }
        
        private void PrivyazatTochkuKObektu(Polyline polyline, int indexTochki, UIElement obj, Point? mousePos = null)
        {
            if (polyline == null || obj == null || indexTochki < 0 || indexTochki >= polyline.Points.Count) return;

            var parent = VisualTreeHelper.GetParent(polyline) as Canvas;
            UIElement strelkaElement = parent != null && parent != HolstSoderzhanie ? (UIElement)parent : polyline;
            
            double canvasLeft = 0, canvasTop = 0;
            Point absTochka;

            if (parent != null && parent != HolstSoderzhanie)
            {
                canvasLeft = Canvas.GetLeft(parent); if (double.IsNaN(canvasLeft)) canvasLeft = 0;
                canvasTop = Canvas.GetTop(parent); if (double.IsNaN(canvasTop)) canvasTop = 0;
                absTochka = new Point(polyline.Points[indexTochki].X + canvasLeft, polyline.Points[indexTochki].Y + canvasTop);
            }
            else
            {
                absTochka = polyline.Points[indexTochki];
            }

<<<<<<< Updated upstream
            var tochkaPrikrepleniya = NaytiTochkuNaGranitse(absTochka, obj);
=======
            // Используем позицию мыши, если она предоставлена, иначе используем текущую позицию точки
            Point tochkaDlyaPrikrepleniya = mousePos ?? absTochka;
            
            // Вычисляем и сохраняем относительную позицию на контуре
            var otnositelnayaPozitsiya = VychislitOtnositelnuyuPozitsiyuNaKonture(tochkaDlyaPrikrepleniya, obj);
            var key = new Tuple<UIElement, int>(strelkaElement, indexTochki);
            tochkiPrikrepleniya[key] = new Tuple<UIElement, Point>(obj, otnositelnayaPozitsiya);
            
            // Восстанавливаем точку на контуре по сохраненной относительной позиции
            Point tochkaPrikrepleniya;
            // Для акторов используем центр объекта, для остальных - точку на контуре
            if (YavlyaetsyaAktorom(obj))
            {
                var bounds = PoluchitGranitsyElementa(obj);
                tochkaPrikrepleniya = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            }
            else
            {
                tochkaPrikrepleniya = VosstanovitTochkuNaKonture(otnositelnayaPozitsiya, obj);
            }
>>>>>>> Stashed changes

            if (parent != null && parent != HolstSoderzhanie)
            {
                var newRelativePoint = new Point(tochkaPrikrepleniya.X - canvasLeft, tochkaPrikrepleniya.Y - canvasTop);
                polyline.Points[indexTochki] = newRelativePoint;
                
                if (polyline.StrokeDashArray == null || polyline.StrokeDashArray.Count == 0)
                    ObnovitStrelkuObobsheniya(parent, polyline.Points);
                else
                    ObnovitStrelkuDlyaCanvas(parent, polyline.Points);
            }
            else
            {
                polyline.Points[indexTochki] = tochkaPrikrepleniya;
            }

            if (markeriIzgiba != null && indexTochki < markeriIzgiba.Count)
            {
                var marker = markeriIzgiba[indexTochki];
                Canvas.SetLeft(marker, tochkaPrikrepleniya.X - marker.Width / 2);
                Canvas.SetTop(marker, tochkaPrikrepleniya.Y - marker.Height / 2);
            }

            var isPervayaTochka = indexTochki == 0;
            var isPoslednyayaTochka = indexTochki == polyline.Points.Count - 1;
            
            if (prikreplennyeStrelki.ContainsKey(strelkaElement))
            {
                var current = prikreplennyeStrelki[strelkaElement];
                if (isPervayaTochka)
                    prikreplennyeStrelki[strelkaElement] = new Tuple<UIElement, UIElement>(obj, current.Item2);
                else if (isPoslednyayaTochka)
                    prikreplennyeStrelki[strelkaElement] = new Tuple<UIElement, UIElement>(current.Item1, obj);
            }
            else
            {
                if (isPervayaTochka)
                    prikreplennyeStrelki[strelkaElement] = new Tuple<UIElement, UIElement>(obj, null);
                else if (isPoslednyayaTochka)
                    prikreplennyeStrelki[strelkaElement] = new Tuple<UIElement, UIElement>(null, obj);
            }
        }

        private void OtdelitTochkuOtObekta(Polyline polyline, int indexTochki)
        {
            if (polyline == null || indexTochki < 0 || indexTochki >= polyline.Points.Count) return;

            var parent = VisualTreeHelper.GetParent(polyline) as Canvas;
            UIElement strelkaElement = parent != null && parent != HolstSoderzhanie ? (UIElement)parent : polyline;
            
            // Удаляем сохраненную точку привязки
            var key = new Tuple<UIElement, int>(strelkaElement, indexTochki);
            if (tochkiPrikrepleniya.ContainsKey(key))
            {
                tochkiPrikrepleniya.Remove(key);
            }

            if (!prikreplennyeStrelki.ContainsKey(strelkaElement)) return;
            var current = prikreplennyeStrelki[strelkaElement];
            var isPervayaTochka = indexTochki == 0;
            var isPoslednyayaTochka = indexTochki == polyline.Points.Count - 1;

            if (isPervayaTochka)
                current = new Tuple<UIElement, UIElement>(null, current.Item2);
            else if (isPoslednyayaTochka)
                current = new Tuple<UIElement, UIElement>(current.Item1, null);

            if (current.Item1 == null && current.Item2 == null)
                prikreplennyeStrelki.Remove(strelkaElement);
            else
                prikreplennyeStrelki[strelkaElement] = current;
        }
    }
}
