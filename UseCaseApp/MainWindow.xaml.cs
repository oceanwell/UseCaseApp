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
        private readonly Stack<UIElement> undoStack = new Stack<UIElement>();
        private readonly Stack<UIElement> redoStack = new Stack<UIElement>();
        //private double currentLineThickness = 2.0;
        private const double standardLineThickness = 1.0;
        private double currentLineThickness = 2.0;

        private Point dragStartPoint;
        private Button sourceButton;
        private bool draggingFromPanel;

        private UIElement selectedElement;
        private List<UIElement> selectedElements = new List<UIElement>();
        private Dictionary<UIElement, double> originalThicknesses = new Dictionary<UIElement, double>();
        private Dictionary<UIElement, Rect> originalSizes = new Dictionary<UIElement, Rect>();
        private Point moveStartPoint;
        private bool movingElement;
        private double originalLeft;
        private double originalTop;

        private bool movingCanvas;
        private Point canvasMoveStartPoint;

        // Переменные для масштабирования
        private Border selectionFrame;
        private List<Border> scaleMarkers;
        private bool scalingElement;
        private Border activeMarker;
        private Point scaleStartPoint;
        private Rect originalSize;
        private Point originalPosition;
        private UIElement elementToScale;

        // Переменные для точек изгиба линий
        private List<Border> bendMarkers;
        private Polyline currentBendLine;
        private int activeBendPoint = -1;
        private bool movingBendPoint;

        private string currentFilePath;
        private bool hasUnsavedChanges;
        private bool blockChangeTracking;
        private bool elementWasMoved;
        private bool elementWasScaled;
        private readonly Dictionary<Line, LineCoordinates> originalLineCoordinates = new Dictionary<Line, LineCoordinates>();
        private ScaleTransform gridScaleTransform;
        private TranslateTransform gridTranslateTransform;
        private DrawingBrush individualGridBrush;

        // Храним прикрепленные стрелки: стрелка -> (начало, конец)
        private Dictionary<UIElement, Tuple<UIElement, UIElement>> attachedArrows = new Dictionary<UIElement, Tuple<UIElement, UIElement>>();

        // Подсветка объектов при приближении стрелки
        private List<Border> objectHighlights = new List<Border>();
        private UIElement firstObjectHighlight = null;
        private UIElement secondObjectHighlight = null;

        public MainWindow()
        {
            InitializeComponent();

            ThicknessText.Text = currentLineThickness.ToString();
            Closing += MainWindow_Closing;
            Loaded += MainWindow_Loaded;
            MarkDocumentClean();
        }


        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetupGrid();
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
            if (gridScaleTransform != null)
            {
                var animXForGrid = animationX.Clone();
                var animYForGrid = animationY.Clone();
                gridScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animXForGrid);
                gridScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animYForGrid);
            }
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

            MarkDocumentDirty();
        }

        private void SetupGrid()
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
                gridScaleTransform = new ScaleTransform(1, 1); // Фиксированный масштаб 1:1
                setkaTranslateTransform = new TranslateTransform(0, 0);

                var transformGroup = new TransformGroup();
                transformGroup.Children.Add(gridScaleTransform);
                transformGroup.Children.Add(setkaTranslateTransform);
                individualnyySetochnyyBrush.Transform = transformGroup;
            }

            FonSetki.Fill = individualnyySetochnyyBrush;
        }

        private void DecreaseThickness_Click(object sender, RoutedEventArgs e)
        {
            if (currentLineThickness > 1)
            {
                currentLineThickness--;
                ThicknessText.Text = currentLineThickness.ToString();
                ObnovitTolshinuLinii();
            }
        }

        private void IncreaseThickness_Click(object sender, RoutedEventArgs e)
        {
            if (currentLineThickness < 10)
            {
                currentLineThickness++;
                ThicknessText.Text = currentLineThickness.ToString();
                ObnovitTolshinuLinii();
            }
        }

        private void ObnovitTolshinuLinii()
        {
            if (selectedElements == null || selectedElements.Count == 0) return;
            bool byliIzmeneniya = false;
            foreach (var element in selectedElements.ToList())
            {
                if (element is Shape forma)
                {
                    forma.StrokeThickness = currentLineThickness;
                    originalThicknesses[element] = currentLineThickness;
                    byliIzmeneniya = true;
                }
                else if (element is Canvas canvas)
                {
                    foreach (var docherniy in canvas.Children.OfType<Shape>())
                    {
                        docherniy.StrokeThickness = currentLineThickness;
                        var key = docherniy as UIElement;
                        if (key != null)
                        {
                            originalThicknesses[key] = currentLineThickness;
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
            if (draggingFromPanel)
            {
                return;
            }

            // Проверяем, не кликнули ли мы на маркер масштабирования или изгиба
            if (e.OriginalSource is Border marker)
            {
                if (scaleMarkers != null && scaleMarkers.Contains(marker))
                {
                    return; // Маркер масштабирования обработает событие сам
                }
                if (bendMarkers != null && bendMarkers.Contains(marker))
                {
                    return; // Маркер изгиба обработает событие сам
                }
            }

            var element = e.OriginalSource as UIElement;

            if (element == PoleDlyaRisovaniya || element == FonSetki || element == selectionFrame || element == HolstSoderzhanie)
            {
                SnytVydelenie();
                movingCanvas = true;
                moveStartPointHolsta = e.GetPosition(this);
                Mouse.Capture(PoleDlyaRisovaniya);
                PoleDlyaRisovaniya.Cursor = Cursors.Hand;
                return;
            }

            var roditelskiyElement = NaytiElementNaHolste(element);

            if (e.ClickCount == 2)
            {
                TextBlock textBlockForEdit = null;
                if (roditelskiyElement is TextBlock tb)
                {
                    textBlockForEdit = tb;
                }
                else if (element is TextBlock tb2)
                {
                    textBlockForEdit = tb2;
                }

                if (textBlockForEdit != null)
                {
                    StartTextEditing(textBlockForEdit);
                    e.Handled = true;
                    return;
                }
            }

            // Если кликнули на линию или полилинию, проверяем, нужно ли добавить новую точку изгиба
            if ((roditelskiyElement is Line || roditelskiyElement is Polyline) &&
                currentBendLine != null &&
                selectedElement == roditelskiyElement)
            {
                // Добавляем новую точку изгиба при клике на линию
                var clickPos = e.GetPosition(HolstSoderzhanie);
                DobavitTochkuIzgiba(clickPos);
                e.Handled = true;
                return;
            }

            // Если кликнули на Canvas с extend/include или обобщением, проверяем, нужно ли добавить новую точку изгиба
            if (roditelskiyElement is Canvas canvas &&
                currentBendLine != null &&
                selectedElement == roditelskiyElement)
            {
                // Проверяем, есть ли Polyline внутри Canvas (extend/include или обобщение)
                var polylineInCanvas = canvas.Children.OfType<Polyline>().FirstOrDefault();

                if (polylineInCanvas == currentBendLine)
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

                selectedElement = roditelskiyElement;
                movingElement = true;
                moveStartPoint = e.GetPosition(HolstSoderzhanie);

                var tekushiyLeft = Canvas.GetLeft(selectedElement);
                var tekushiyTop = Canvas.GetTop(selectedElement);
                originalLeft = double.IsNaN(tekushiyLeft) ? 0 : tekushiyLeft;
                originalTop = double.IsNaN(tekushiyTop) ? 0 : tekushiyTop;

                Mouse.Capture(PoleDlyaRisovaniya);

                if (!selectedElements.Contains(selectedElement))
                {
                    selectedElements.Add(selectedElement);
                }

                VydelitElement(selectedElement);
                ObnovitSchetchikTolschiny(selectedElement);


            }
        }

        private void VydelitElement(UIElement element)
        {
            if (element is Shape forma)
            {

                if (!originalThicknesses.ContainsKey(element))
                {
                    originalThicknesses[element] = forma.StrokeThickness;
                }
                // Оранжевый цвет выделения (#CD853F)
                forma.Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CD853F"));
                forma.StrokeThickness = 2;
            }
            else if (element is Canvas canvas)
            {
                // Оранжевый цвет выделения (#CD853F)
                var orangeColor = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CD853F"));
                foreach (var docherniy in canvas.Children.OfType<Shape>())
                {
                    var key = docherniy as UIElement;
                    if (key != null && !originalThicknesses.ContainsKey(key))
                    {
                        originalThicknesses[key] = docherniy.StrokeThickness;
                    }
                    docherniy.Stroke = orangeColor;
                    docherniy.StrokeThickness = 2;
                }
            }

            PokazatRamuMashtabirovaniya(element);
        }

        private void SkrytRamuMashtabirovaniya()
        {
            if (selectionFrame != null && HolstSoderzhanie != null)
            {
                HolstSoderzhanie.Children.Remove(selectionFrame);
                selectionFrame = null;
            }

            if (scaleMarkers != null)
            {
                foreach (var marker in scaleMarkers)
                {
                    if (HolstSoderzhanie != null && HolstSoderzhanie.Children.Contains(marker))
                    {
                        HolstSoderzhanie.Children.Remove(marker);
                    }
                }
                scaleMarkers.Clear();
            }

            SkrytMarkeriIzgiba();
        }

        private void DobavitTochkuIzgiba(Point position)
        {
            if (currentBendLine == null) return;

            var points = currentBendLine.Points;
            if (points == null) return;

            // Проверяем, находится ли Polyline внутри Canvas
            var parent = VisualTreeHelper.GetParent(currentBendLine) as Canvas;
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
                    PokazatMarkeriIzgibaDlyaObobsheniya(parent, currentBendLine);
                }
                else
                {
                    // Это extend/include
                    ObnovitStrelkuDlyaCanvas(parent, points);
                    PokazatMarkeriIzgibaDlyaCanvas(parent, currentBendLine);
                }
            }
            else
            {
                PokazatMarkeriIzgiba(currentBendLine);
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

                // Смещаем текст перпендикулярно линии (вверх)
                var offsetDistance = 12.0; // Расстояние от линии
                var textX = centerPoint.X - offsetDistance * Math.Sin(segmentAngle);
                var textY = centerPoint.Y + offsetDistance * Math.Cos(segmentAngle);

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

                            if (selectedElement == path)
                            {
                                selectedElement = noviyCanvas;
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

            selectionFrame = new Border
            {
                Child = rect,
                Background = Brushes.Transparent,
                IsHitTestVisible = false
            };

            Canvas.SetLeft(selectionFrame, bounds.Left - 4);
            Canvas.SetTop(selectionFrame, bounds.Top - 4);
            Panel.SetZIndex(selectionFrame, 1000);
            if (HolstSoderzhanie != null)
            {
                HolstSoderzhanie.Children.Add(selectionFrame);
            }

            // Обновляем список маркеров перед созданием новых
            if (scaleMarkers == null)
            {
                scaleMarkers = new List<Border>();
            }

            // Создаем маркеры изменения размера (8 штук: 4 угла + 4 стороны)
            scaleMarkers = new List<Border>();
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
                scaleMarkers.Add(marker);
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

                    if (selectedElement == line)
                    {
                        selectedElement = polyline;
                    }
                }

                points = polyline.Points;
            }

            if (polyline == null || points == null || points.Count == 0) return;

            currentBendLine = polyline;

            SozdatMarkeriIzgiba(points, null);
        }

        private void PokazatMarkeriIzgibaDlyaCanvas(Canvas canvas, Polyline polyline)
        {
            SkrytMarkeriIzgiba();

            if (polyline == null || polyline.Points == null || polyline.Points.Count == 0) return;

            currentBendLine = polyline;

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

            currentBendLine = polyline;

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
            if (bendMarkers == null)
            {
                bendMarkers = new List<Border>();
            }

            bendMarkers.Clear();
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
                bendMarkers.Add(marker);
            }
        }

        private void SkrytMarkeriIzgiba()
        {
            if (bendMarkers != null)
            {
                foreach (var marker in bendMarkers)
                {
                    if (HolstSoderzhanie != null && HolstSoderzhanie.Children.Contains(marker))
                    {
                        HolstSoderzhanie.Children.Remove(marker);
                    }
                }
                bendMarkers.Clear();
            }
            currentBendLine = null;
        }

        private void MarkerIzgiba_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (currentBendLine == null) return;

            var border = sender as Border;
            activeBendPoint = (border?.Tag is int index) ? index : -1;
            if (activeBendPoint >= 0)
            {
                movingBendPoint = true;
                Mouse.Capture(PoleDlyaRisovaniya);
                e.Handled = true;
            }
        }

        private void MarkerIzgiba_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (movingBendPoint)
            {
                // Прикрепляем точку к подсвеченному объекту или ближайшему
                if (currentBendLine != null && activeBendPoint >= 0)
                {
                    var points = currentBendLine.Points;
                    if (activeBendPoint < points.Count)
                    {
                        var isPervayaTochka = activeBendPoint == 0;
                        var isPoslednyayaTochka = activeBendPoint == points.Count - 1;

                        UIElement objToAttach = null;
                        if (isPervayaTochka && podsvetkaPervogoObekta != null)
                        {
                            objToAttach = podsvetkaPervogoObekta;
                        }
                        else if (isPoslednyayaTochka && podsvetkaVtorogoObekta != null)
                        {
                            objToAttach = podsvetkaVtorogoObekta;
                        }
                        
                        // Если нет подсвеченного объекта, ищем ближайший
                        if (objToAttach == null && (isPervayaTochka || isPoslednyayaTochka))
                        {
                            var parent = VisualTreeHelper.GetParent(currentBendLine) as Canvas;
                            Point absPoint;
                            if (parent != null && parent != HolstSoderzhanie)
                            {
                                var canvasLeft = Canvas.GetLeft(parent); if (double.IsNaN(canvasLeft)) canvasLeft = 0;
                                var canvasTop = Canvas.GetTop(parent); if (double.IsNaN(canvasTop)) canvasTop = 0;
                                absPoint = new Point(points[activeBendPoint].X + canvasLeft, points[activeBendPoint].Y + canvasTop);
                            }
                            else
                            {
                                absPoint = points[activeBendPoint];
                            }
                            objToAttach = NaytiBlizhayshiyObekt(absPoint);
                        }

                        if (objToAttach != null)
                        {
                            PrivyazatTochkuKObektu(currentBendLine, activeBendPoint, objToAttach);
                        }
                    }
                }

                movingBendPoint = false;
                activeBendPoint = -1;
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
                    if (originalLineCoordinates.TryGetValue(line, out var origCoords))
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
                if (originalThicknesses.ContainsKey(element))
                {
                    tolstina = originalThicknesses[element];
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
                    if (key != null && originalThicknesses.ContainsKey(key))
                    {
                        tolstina = originalThicknesses[key];
                    }
                    else
                    {
                        tolstina = standartnayaTolschinaLinii;
                    }
                }
            }

            currentLineThickness = tolstina;
            if (ThicknessText != null)
            {
                ThicknessText.Text = currentLineThickness.ToString();
            }
        }

        private void SnytVydelenie()
        {
            foreach (var element in selectedElements.ToList())
            {
                if (element is Shape forma)
                {
                    forma.Stroke = Brushes.Black;
                    if (originalThicknesses.ContainsKey(element))
                    {
                        forma.StrokeThickness = originalThicknesses[element];
                        originalThicknesses.Remove(element);
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
                        if (key != null && originalThicknesses.ContainsKey(key))
                        {
                            docherniy.StrokeThickness = originalThicknesses[key];
                            originalThicknesses.Remove(key);
                        }
                        else
                        {
                            docherniy.StrokeThickness = standartnayaTolschinaLinii;
                        }
                    }
                }
            }
            selectedElements.Clear();
            originalThicknesses.Clear();
            SkrytRamuMashtabirovaniya();
        }

        private void Marker_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (selectedElement == null) return;

            activeMarker = sender as Border;
            if (activeMarker == null) return;

            scalingElement = true;
            elementToScale = selectedElement;
            scaleStartPoint = e.GetPosition(HolstSoderzhanie);

            // Получаем текущие размеры элемента (с учетом масштабирования)
            var currentBounds = PoluchitGranitsyElementa(elementToScale);

            // Для Line элементов координаты задаются через X1/Y1/X2/Y2, а не через Canvas.GetLeft/Top
            // Поэтому используем границы из currentBounds
            double realLeft, realTop;
            if (elementToScale is Line)
            {
                // Для Line используем границы из PoluchitGranitsyElementa
                realLeft = currentBounds.Left;
                realTop = currentBounds.Top;
            }
            else
            {
                // Для других элементов получаем РЕАЛЬНУЮ позицию на Canvas
                realLeft = Canvas.GetLeft(elementToScale);
                realTop = Canvas.GetTop(elementToScale);
                if (double.IsNaN(realLeft)) realLeft = 0;
                if (double.IsNaN(realTop)) realTop = 0;
            }

            // Сохраняем оригинальные размеры при первом масштабировании
            if (!originalSizes.ContainsKey(elementToScale))
            {
                var realBounds = PoluchitGranitsyBezMashtaba(elementToScale);
                originalSizes[elementToScale] = realBounds;
            }

            // Используем текущие размеры и позицию элемента
            // Это предотвращает перемещение элемента при нажатии на маркер
            originalSize = new Rect(realLeft, realTop, currentBounds.Width, currentBounds.Height);
            originalPosition = new Point(realLeft, realTop);

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
            if (scalingElement)
            {
                scalingElement = false;
                if (activeMarker != null)
                {
                    activeMarker.ReleaseMouseCapture();
                }
                activeMarker = null;
                Mouse.Capture(null);

                if (elementToScale != null && elementWasScaled)
                {
                    PokazatRamuMashtabirovaniya(elementToScale);
                    MarkDocumentDirty();
                    elementWasScaled = false;
                }
            }
        }

        private void MashtabirovatElement(UIElement element, double left, double top, double width, double height)
        {
            if (element == null || width <= 0 || height <= 0) return;

            // Сохраняем оригинальные размеры при первом масштабировании
            if (!originalSizes.ContainsKey(element))
            {
                var realBounds = PoluchitGranitsyBezMashtaba(element);
                originalSizes[element] = realBounds;
            }

            // Получаем оригинальные размеры для вычисления финального масштаба
            var baseBounds = originalSizes[element];
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
                    if (!originalLineCoordinates.ContainsKey(lineForScale))
                    {
                        originalLineCoordinates[lineForScale] = new LineCoordinates(
                            lineForScale.X1,
                            lineForScale.Y1,
                            lineForScale.X2,
                            lineForScale.Y2
                        );
                    }

                    // Растягиваем линию к новым границам
                    var origCoords = originalLineCoordinates[lineForScale];
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
                    if (!originalSizes.ContainsKey(element))
                    {
                        // Сохраняем текущие размеры
                        var currentBounds = PoluchitGranitsyBezMashtaba(path);
                        originalSizes[element] = currentBounds;
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
            // Для TextBlock изменяем Width и Height напрямую
            else if (element is TextBlock textBlock)
            {
                if (!originalSizes.ContainsKey(element))
                {
                    textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    originalSizes[element] = new Rect(left, top, textBlock.DesiredSize.Width, textBlock.DesiredSize.Height);
                }

                var baseRect = originalSizes[element];
                textBlock.Width = width;
                textBlock.TextWrapping = TextWrapping.Wrap;
                textBlock.TextAlignment = TextAlignment.Center;
                textBlock.VerticalAlignment = VerticalAlignment.Center;
                textBlock.HorizontalAlignment = HorizontalAlignment.Center;
                
                textBlock.Height = double.NaN;
                textBlock.Padding = new Thickness(0);
                textBlock.Measure(new Size(width, double.PositiveInfinity));
                var textHeight = textBlock.DesiredSize.Height;
                
                if (textHeight < height)
                {
                    var verticalPadding = (height - textHeight) / 2.0;
                    textBlock.Padding = new Thickness(0, verticalPadding, 0, verticalPadding);
                }
                
                textBlock.Height = height;
                textBlock.Measure(new Size(width, height));
                textBlock.Arrange(new Rect(0, 0, width, height));
                
                Canvas.SetLeft(textBlock, left);
                Canvas.SetTop(textBlock, top);
            }
            else if (element is FrameworkElement fe)
            {
                if (!originalSizes.ContainsKey(element))
                {
                    fe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    originalSizes[element] = new Rect(left, top, fe.DesiredSize.Width, fe.DesiredSize.Height);
                }

                var baseRect = originalSizes[element];
                fe.Width = baseRect.Width * finalScaleX;
                fe.Height = baseRect.Height * finalScaleY;
                Canvas.SetLeft(fe, left);
                Canvas.SetTop(fe, top);
            }
        }

        private void PoleDlyaRisovaniya_DragOver(object sender, DragEventArgs e)
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

        private void PoleDlyaRisovaniya_MouseMove(object sender, MouseEventArgs e)
        {
            // Если перетаскиваем точку изгиба
            if (movingBendPoint && activeBendPoint >= 0 && currentBendLine != null)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    var newPos = e.GetPosition(HolstSoderzhanie);
                    var points = currentBendLine.Points;
                    if (activeBendPoint < points.Count)
                    {
                        // Проверяем, находится ли Polyline внутри Canvas
                        var parent = VisualTreeHelper.GetParent(currentBendLine) as Canvas;
                        if (parent != null && parent != HolstSoderzhanie)
                        {
                            // Polyline внутри Canvas - координаты относительные
                            var canvasLeft = Canvas.GetLeft(parent);
                            var canvasTop = Canvas.GetTop(parent);
                            if (double.IsNaN(canvasLeft)) canvasLeft = 0;
                            if (double.IsNaN(canvasTop)) canvasTop = 0;

                            var relativePos = new Point(newPos.X - canvasLeft, newPos.Y - canvasTop);
                            points[activeBendPoint] = relativePos;

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
                            points[activeBendPoint] = newPos;
                        }

                        // Обновляем позицию маркера
                        if (bendMarkers != null && activeBendPoint < bendMarkers.Count)
                        {
                            var marker = bendMarkers[activeBendPoint];
                            Canvas.SetLeft(marker, newPos.X - marker.Width / 2);
                            Canvas.SetTop(marker, newPos.Y - marker.Height / 2);
                        }

                        // Подсветка убрана по запросу пользователя
                    }
                }
                return;
            }

            // Если масштабируем, обрабатываем масштабирование
            if (scalingElement && activeMarker != null && elementToScale != null)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    var tekushayaPoz = e.GetPosition(HolstSoderzhanie);
                    var deltaX = tekushayaPoz.X - scaleStartPoint.X;
                    var deltaY = tekushayaPoz.Y - scaleStartPoint.Y;

                    // Получаем индекс маркера из Tag
                    int markerIndex = -1;
                    if (activeMarker.Tag is int index)
                    {
                        markerIndex = index;
                    }
                    else if (scaleMarkers != null)
                    {
                        markerIndex = scaleMarkers.IndexOf(activeMarker);
                    }

                    if (markerIndex >= 0 && markerIndex < 8)
                    {
                        double newWidth = originalSize.Width;
                        double newHeight = originalSize.Height;
                        double newLeft = originalPosition.X;
                        double newTop = originalPosition.Y;

                        // Вычисляем новые размеры и позицию в зависимости от маркера
                        switch (markerIndex)
                        {
                            case 0: // Левый верхний
                                newWidth = Math.Max(20, originalSize.Width - deltaX);
                                newHeight = Math.Max(20, originalSize.Height - deltaY);
                                newLeft = originalPosition.X + (originalSize.Width - newWidth);
                                newTop = originalPosition.Y + (originalSize.Height - newHeight);
                                break;
                            case 1: // Правый верхний
                                newWidth = Math.Max(20, originalSize.Width + deltaX);
                                newHeight = Math.Max(20, originalSize.Height - deltaY);
                                newTop = originalPosition.Y + (originalSize.Height - newHeight);
                                break;
                            case 2: // Левый нижний
                                newWidth = Math.Max(20, originalSize.Width - deltaX);
                                newHeight = Math.Max(20, originalSize.Height + deltaY);
                                newLeft = originalPosition.X + (originalSize.Width - newWidth);
                                break;
                            case 3: // Правый нижний
                                newWidth = Math.Max(20, originalSize.Width + deltaX);
                                newHeight = Math.Max(20, originalSize.Height + deltaY);
                                break;
                            case 4: // Верхний центр
                                newHeight = Math.Max(20, originalSize.Height - deltaY);
                                newTop = originalPosition.Y + (originalSize.Height - newHeight);
                                break;
                            case 5: // Правый центр
                                newWidth = Math.Max(20, originalSize.Width + deltaX);
                                break;
                            case 6: // Нижний центр
                                newHeight = Math.Max(20, originalSize.Height + deltaY);
                                break;
                            case 7: // Левый центр
                                newWidth = Math.Max(20, originalSize.Width - deltaX);
                                newLeft = originalPosition.X + (originalSize.Width - newWidth);
                                break;
                        }

                        // Применяем масштабирование только если размеры валидны
                        if (newWidth > 0 && newHeight > 0 && originalSize.Width > 0 && originalSize.Height > 0)
                        {
                            MashtabirovatElement(elementToScale, newLeft, newTop, newWidth, newHeight);
                            PokazatRamuMashtabirovaniya(elementToScale);
                            elementWasScaled = true;
                        }
                    }
                }
                else
                {
                    scalingElement = false;
                    activeMarker = null;
                    Mouse.Capture(null);
                }
                return;
            }

            if (movingCanvas)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    var tekushayaPoz = e.GetPosition(this);
                    var deltaX = tekushayaPoz.X - moveStartPointHolsta.X;
                    var deltaY = tekushayaPoz.Y - moveStartPointHolsta.Y;

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

                    moveStartPointHolsta = tekushayaPoz;
                }
                else
                {
                    movingCanvas = false;
                    Mouse.Capture(null);
                    PoleDlyaRisovaniya.Cursor = Cursors.Arrow;
                }
                return;
            }

            if (movingElement && selectedElement != null)
            {
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    var tekushayaPoz = e.GetPosition(HolstSoderzhanie);

                    var smeshenieX = tekushayaPoz.X - moveStartPoint.X;
                    var smeshenieY = tekushayaPoz.Y - moveStartPoint.Y;

                    Canvas.SetLeft(selectedElement, originalLeft + smeshenieX);
                    Canvas.SetTop(selectedElement, originalTop + smeshenieY);

                    // Обновляем рамку масштабирования при перемещении
                    PokazatRamuMashtabirovaniya(selectedElement);

                    // Если перемещаем объект - обновляем прикрепленные стрелки
                    if (!YavlyaetsyaStrelkoy(selectedElement))
                        ObnovitStrelkiDlyaObekta(selectedElement);

                    elementWasMoved = true;
                }
                else
                {
                    movingElement = false;
                    Mouse.Capture(null);
                    selectedElement = null;
                }
            }
        }

        private void PoleDlyaRisovaniya_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (scalingElement)
            {
                scalingElement = false;
                activeMarker = null;
                Mouse.Capture(null);
                if (elementToScale != null)
                {
                    PokazatRamuMashtabirovaniya(elementToScale);
                }
                if (elementWasScaled)
                {
                    elementWasScaled = false;
                    MarkDocumentDirty();
                }
                else
                {
                    elementWasScaled = false;
                }
                return;
            }

            if (movingCanvas)
            {
                movingCanvas = false;
                Mouse.Capture(null);
                PoleDlyaRisovaniya.Cursor = Cursors.Arrow;
            }

            // Обрабатываем прикрепление точки изгиба, если мышь была отпущена на холсте
            if (movingBendPoint)
            {
                if (currentBendLine != null && activeBendPoint >= 0)
                {
                    var points = currentBendLine.Points;
                    if (activeBendPoint < points.Count)
                    {
                        var isPervayaTochka = activeBendPoint == 0;
                        var isPoslednyayaTochka = activeBendPoint == points.Count - 1;

                        UIElement objToAttach = null;
                        if (isPervayaTochka && podsvetkaPervogoObekta != null)
                        {
                            objToAttach = podsvetkaPervogoObekta;
                        }
                        else if (isPoslednyayaTochka && podsvetkaVtorogoObekta != null)
                        {
                            objToAttach = podsvetkaVtorogoObekta;
                        }
                        
                        // Если нет подсвеченного объекта, ищем ближайший
                        if (objToAttach == null && (isPervayaTochka || isPoslednyayaTochka))
                        {
                            var mousePos = e.GetPosition(HolstSoderzhanie);
                            objToAttach = NaytiBlizhayshiyObekt(mousePos);
                        }

                        if (objToAttach != null)
                        {
                            PrivyazatTochkuKObektu(currentBendLine, activeBendPoint, objToAttach);
                        }
                    }
                }

                movingBendPoint = false;
                activeBendPoint = -1;
                SkrytPodsvetku();
                Mouse.Capture(null);
                MarkDocumentDirty();
            }

            if (movingElement)
            {
                movingElement = false;
                Mouse.Capture(null);

                if (selectedElement != null)
                {
                    PokazatRamuMashtabirovaniya(selectedElement);
                    // Скрываем подсветку, если не перемещали стрелку
                    if (!YavlyaetsyaStrelkoy(selectedElement))
                        SkrytPodsvetku();
                }
            }

            if (elementWasMoved)
            {
                elementWasMoved = false;
                MarkDocumentDirty();
            }
            else
            {
                elementWasMoved = false;
            }
        }

        private void PoleDlyaRisovaniya_Drop(object sender, DragEventArgs e)
        {
            if (movingElement || movingCanvas || !draggingFromPanel)
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
                DobavitNaHolst(element);
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
                if (selectionFrame != null && ReferenceEquals(child, selectionFrame)) continue;
                if (scaleMarkers != null && child is Border marker && scaleMarkers.Contains(marker)) continue;

                element = child;
                break;
            }

            if (element == null) return;

            // Если удаляем выбранный элемент, скрываем рамку и маркеры
            if (selectedElement == element || (selectedElement == null && selectedElements.Contains(element)))
            {
                SkrytRamuMashtabirovaniya();
                SnytVydelenie();
            }

            HolstSoderzhanie.Children.Remove(element);
            undoStack.Push(element);
            redoStack.Clear();
            MarkDocumentDirty();
        }

        private void Vozvrat_Click(object sender, RoutedEventArgs e)
        {
            if (undoStack.Count == 0) return;
            var element = undoStack.Pop();
            HolstSoderzhanie.Children.Add(element);
            redoStack.Push(element);

            // Если это был выбранный элемент, обновляем рамку и маркеры
            if (selectedElement == element || (selectedElement == null && selectedElements.Contains(element)))
            {
                selectedElement = element;
                PokazatRamuMashtabirovaniya(element);
            }
            MarkDocumentDirty();
        }

        private void DobavitNaHolst(UIElement element, bool otslezhivatIzmeneniya = true)
        {
            if (HolstSoderzhanie == null || element == null) return;

            HolstSoderzhanie.Children.Add(element);
            redoStack.Clear();

            // Сохраняем оригинальные размеры при добавлении элемента
            // Сохраняем координаты для Line элементов сразу
            if (element is Line line)
            {
                if (!originalLineCoordinates.ContainsKey(line))
                {
                    originalLineCoordinates[line] = new LineCoordinates(line.X1, line.Y1, line.X2, line.Y2);
                }
            }

            // Немного задержки, чтобы элемент успел отрендериться
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var bounds = PoluchitGranitsyBezMashtaba(element);
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    originalSizes[element] = bounds;
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

        private UIElement CreateActor()
        {
            var group = new Canvas();

            // Голова актора - черная
            var head = new Ellipse { Width = 30, Height = 30, Stroke = Brushes.Black, StrokeThickness = standardLineThickness, Fill = Brushes.Black };
            Canvas.SetLeft(head, 50);
            Canvas.SetTop(head, 30);

            // Тело, руки и ноги актора - черные
            var body = new Line { X1 = 65, Y1 = 60, X2 = 65, Y2 = 120, Stroke = Brushes.Black, StrokeThickness = standardLineThickness };
            var leftArm = new Line { X1 = 35, Y1 = 80, X2 = 95, Y2 = 80, Stroke = Brushes.Black, StrokeThickness = standardLineThickness };
            var leftLeg = new Line { X1 = 65, Y1 = 120, X2 = 45, Y2 = 150, Stroke = Brushes.Black, StrokeThickness = standardLineThickness };
            var rightLeg = new Line { X1 = 65, Y1 = 120, X2 = 85, Y2 = 150, Stroke = Brushes.Black, StrokeThickness = standardLineThickness };

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
                case "actor":
                    {
                        var actor = CreateActor();
                        Canvas.SetLeft(actor, point.X - 65);
                        Canvas.SetTop(actor, point.Y - 90);
                        return actor;
                    }
                case "useCase":
                    {
                        var ellipse = new Ellipse
                        {
                            Width = 120,
                            Height = 60,
                            Stroke = Brushes.Black,
                            //StrokeThickness = currentLineThickness,
                            StrokeThickness = standardLineThickness,
                            Fill = Brushes.White
                        };
                        Canvas.SetLeft(ellipse, point.X - 60);
                        Canvas.SetTop(ellipse, point.Y - 30);
                        return ellipse;
                    }
                case "system":
                    {
                        var rectangle = new Rectangle
                        {
                            Width = 240,
                            Height = 160,
                            Stroke = Brushes.Black,
                            //StrokeThickness = currentLineThickness,
                            StrokeThickness = standardLineThickness,
                            Fill = Brushes.Transparent,
                            RadiusX = 4,
                            RadiusY = 4
                        };
                        Canvas.SetLeft(rectangle, point.X - 120);
                        Canvas.SetTop(rectangle, point.Y - 80);
                        return rectangle;
                    }
                case "line":
                    {
                        var line = new Polyline
                        {
                            Points = new PointCollection { new Point(point.X, point.Y), new Point(point.X + 120, point.Y + 60) },
                            Stroke = Brushes.Black,
                            //StrokeThickness = currentLineThickness
                            StrokeThickness = standardLineThickness
                        };
                        return line;
                    }
                case "include":
                    {
                        var group = new Canvas();
                        var line = new Polyline
                        {
                            Points = new PointCollection { new Point(0, 20), new Point(130, 20) },
                            Stroke = Brushes.Black,
                            //StrokeThickness = currentLineThickness,
                            StrokeThickness = standardLineThickness,
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
                case "extend":
                    {
                        var group = new Canvas();
                        var line = new Polyline
                        {
                            Points = new PointCollection { new Point(0, 20), new Point(130, 20) },
                            Stroke = Brushes.Black,
                            //StrokeThickness = currentLineThickness,
                            StrokeThickness = standardLineThickness,
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
                case "generalization":
                    {
                        var group = new Canvas();
                        var line = new Polyline
                        {
                            Points = new PointCollection { new Point(0, 20), new Point(100, 20) },
                            Stroke = Brushes.Black,
                            StrokeThickness = standardLineThickness
                        };
                        var arrow = new System.Windows.Shapes.Polygon
                        {
                            Points = new PointCollection { new Point(100, 20), new Point(100, 10), new Point(120, 20), new Point(100, 30) },
                            Fill = Brushes.White,
                            Stroke = Brushes.Black,
                            StrokeThickness = standardLineThickness
                        };
                        group.Children.Add(line);
                        group.Children.Add(arrow);
                        Canvas.SetLeft(group, point.X - 60);
                        Canvas.SetTop(group, point.Y - 20);
                        return group;
                    }
                case "text":
                    {
                        var textBlock = new TextBlock 
                        { 
                            Text = "Текст", 
                            FontSize = 16, 
                            Foreground = Brushes.Black,
                            TextAlignment = TextAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Center
                        };
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

            string targetPath = currentFilePath;
            if (prinuditelnoyeVyborMesta || string.IsNullOrWhiteSpace(targetPath))
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "Use Case App (*.uca)|*.uca|Все файлы (*.*)|*.*",
                    DefaultExt = "uca",
                    AddExtension = true,
                    Title = "Сохранить диаграмму",
                    FileName = string.IsNullOrWhiteSpace(currentFilePath) ? "Диаграмма" : System.IO.Path.GetFileName(currentFilePath)
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

            currentFilePath = targetPath;
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
                if (selectionFrame != null && ReferenceEquals(child, selectionFrame)) continue;
                if (scaleMarkers != null && child is Border marker && scaleMarkers.Contains(marker)) continue;

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

                blockChangeTracking = true;
                try
                {
                    OchistitHolstCore();
                    PriminitDiagrammu(diagram);
                }
                finally
                {
                    blockChangeTracking = false;
                }

                currentFilePath = filePath;
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
            SnytVydelenie();
            SkrytRamuMashtabirovaniya();
            HolstSoderzhanie?.Children.Clear();
            undoStack.Clear();
            redoStack.Clear();
            originalSizes.Clear();
            originalThicknesses.Clear();
            originalLineCoordinates.Clear();
            selectedElement = null;
            selectedElements.Clear();
            elementWasMoved = false;
            elementWasScaled = false;
        }

        private void SozdatNovyyDokument()
        {
            blockChangeTracking = true;
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
                if (gridScaleTransform != null)
                {
                    gridScaleTransform.ScaleX = 1;
                    gridScaleTransform.ScaleY = 1;
                }

                if (PolzunokMashtaba != null)
                {
                    PolzunokMashtaba.Value = 100;
                }

                if (PerekyuchatelSetki != null)
                {
                    PerekyuchatelSetki.IsChecked = true;
                }
                else if (FonSetki != null)
                {
                    FonSetki.Visibility = Visibility.Visible;
                }

                if (ThicknessText != null)
                {
                    ThicknessText.Text = currentLineThickness.ToString();
                }
            }
            finally
            {
                blockChangeTracking = false;
            }

            currentFilePath = null;
            MarkDocumentClean();
        }

        private bool ProveritNuzhnoLiSohranitPeredDeystviem()
        {
            if (!hasUnsavedChanges)
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
            if (blockChangeTracking)
            {
                return;
            }

            hasUnsavedChanges = true;
            ObnovitZagolovokOkna();
        }

        private void MarkDocumentClean()
        {
            hasUnsavedChanges = false;
            ObnovitZagolovokOkna();
        }

        private void ObnovitZagolovokOkna()
        {
            var fileName = string.IsNullOrWhiteSpace(currentFilePath) ? "Безымянный" : System.IO.Path.GetFileName(currentFilePath);
            Title = hasUnsavedChanges ? $"UCA - {fileName}*" : $"UCA - {fileName}";
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

            Tuple<UIElement, UIElement> savedAttachments = null;
            if (prikreplennyeStrelki.ContainsKey(strelka))
            {
                savedAttachments = prikreplennyeStrelki[strelka];
            }

            var p1 = polyline.Points[0];
            var p2 = polyline.Points[polyline.Points.Count - 1];
            if (canvas != null)
            {
                p1 = new Point(p1.X + canvasLeft, p1.Y + canvasTop);
                p2 = new Point(p2.X + canvasLeft, p2.Y + canvasTop);
            }

            var obj1 = savedAttachments?.Item1 ?? NaytiBlizhayshiyObekt(p1);
            var obj2 = savedAttachments?.Item2 ?? NaytiBlizhayshiyObekt(p2);

            bool updated = false;
            if (obj1 != null)
            {
                var t = NaytiTochkuNaGranitseOtTsentra(p1, obj1);
                if (canvas != null)
                    polyline.Points[0] = new Point(t.X - canvasLeft, t.Y - canvasTop);
                else
                    polyline.Points[0] = t;
                updated = true;
            }

            if (obj2 != null)
            {
                var t = NaytiTochkuNaGranitseOtTsentra(p2, obj2);
                var idx = polyline.Points.Count - 1;
                if (canvas != null)
                    polyline.Points[idx] = new Point(t.X - canvasLeft, t.Y - canvasTop);
                else
                    polyline.Points[idx] = t;
                updated = true;
            }

            if (updated && canvas != null)
                ObnovitStrelku(canvas, polyline);

            if (bendMarkers != null && polyline != null)
            {
                var points = polyline.Points;
                for (int i = 0; i < points.Count && i < bendMarkers.Count; i++)
                {
                    var marker = bendMarkers[i];
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
                if (el == selectionFrame) continue;
                if (scaleMarkers != null && scaleMarkers.Contains(el)) continue;
                if (bendMarkers != null && bendMarkers.Contains(el)) continue;
                if (podsvetkiObektov != null && podsvetkiObektov.Contains(el)) continue;

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

        private Point NaytiTochkuNaGranitse(Point p, UIElement obj)
        {
            var bounds = PoluchitGranitsyBezMashtaba(obj);
            var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            var dx = p.X - center.X;
            var dy = p.Y - center.Y;
            
            if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
                return new Point(bounds.Right, center.Y);
            
            var ratio = Math.Min(Math.Abs(bounds.Width / 2 / dx), Math.Abs(bounds.Height / 2 / dy));
            var x = center.X + dx * ratio;
            var y = center.Y + dy * ratio;
            
            if (Math.Abs(x - bounds.Left) < 1 || Math.Abs(x - bounds.Right) < 1)
                return new Point(Math.Abs(x - bounds.Left) < 1 ? bounds.Left : bounds.Right, y);
            else
                return new Point(x, Math.Abs(y - bounds.Top) < 1 ? bounds.Top : bounds.Bottom);
        }

        private Point NaytiTochkuNaGranitseOtTsentra(Point p, UIElement obj)
        {
            var bounds = PoluchitGranitsyBezMashtaba(obj);
            var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            var dx = p.X - center.X;
            var dy = p.Y - center.Y;
            
            if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
                return new Point(bounds.Right, center.Y);
            
            var length = Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.001) return new Point(bounds.Right, center.Y);
            
            var dirX = dx / length;
            var dirY = dy / length;
            
            var halfWidth = bounds.Width / 2;
            var halfHeight = bounds.Height / 2;
            
            double tX = double.MaxValue;
            if (Math.Abs(dirX) > 0.001)
            {
                if (dirX > 0)
                    tX = halfWidth / dirX;
                else
                    tX = -halfWidth / dirX;
            }
            
            double tY = double.MaxValue;
            if (Math.Abs(dirY) > 0.001)
            {
                if (dirY > 0)
                    tY = halfHeight / dirY;
                else
                    tY = -halfHeight / dirY;
            }
            
            var t = Math.Min(tX, tY);
            var x = center.X + dirX * t;
            var y = center.Y + dirY * t;
            
            x = Math.Max(bounds.Left, Math.Min(bounds.Right, x));
            y = Math.Max(bounds.Top, Math.Min(bounds.Bottom, y));
            
            return new Point(x, y);
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
        private void PrivyazatTochkuKObektu(Polyline polyline, int indexTochki, UIElement obj)
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

            var tochkaPrikrepleniya = NaytiTochkuNaGranitse(absTochka, obj);

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

            if (bendMarkers != null && indexTochki < bendMarkers.Count)
            {
                var marker = bendMarkers[indexTochki];
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

        private void StartTextEditing(TextBlock textBlock)
        {
            if (textBlock == null) return;

            var parent = VisualTreeHelper.GetParent(textBlock) as Panel;
            if (parent == null || !parent.Children.Contains(textBlock)) return;

            var left = Canvas.GetLeft(textBlock);
            var top = Canvas.GetTop(textBlock);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            var originalText = textBlock.Text;
            var originalFontSize = textBlock.FontSize;
            var originalFontFamily = textBlock.FontFamily;
            var originalForeground = textBlock.Foreground;
            
            var originalWidth = textBlock.Width;
            var originalHeight = textBlock.Height;
            
            if (double.IsNaN(originalWidth) || originalWidth <= 0)
            {
                originalWidth = textBlock.ActualWidth;
            }
            if (double.IsNaN(originalHeight) || originalHeight <= 0)
            {
                originalHeight = textBlock.ActualHeight;
            }
            
            if (originalWidth <= 0 || originalHeight <= 0)
            {
                textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                if (originalWidth <= 0) originalWidth = textBlock.DesiredSize.Width;
                if (originalHeight <= 0) originalHeight = textBlock.DesiredSize.Height;
            }
            
            if (originalWidth <= 0) originalWidth = 100;
            if (originalHeight <= 0) originalHeight = originalFontSize + 10;
            
            var textBox = new TextBox
            {
                Text = originalText,
                FontSize = originalFontSize,
                FontFamily = originalFontFamily,
                Foreground = originalForeground,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FocusVisualStyle = null,
                Width = originalWidth,
                Height = originalHeight,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Padding = new Thickness(0),
                TextWrapping = TextWrapping.Wrap,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                TextAlignment = TextAlignment.Center
            };

            Canvas.SetLeft(textBox, left);
            Canvas.SetTop(textBox, top);

            int index = parent.Children.IndexOf(textBlock);
            parent.Children.RemoveAt(index);
            parent.Children.Insert(index, textBox);


            textBox.Focus();
            textBox.SelectAll();

            textBox.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    FinishTextEditing(textBox, originalText, originalFontSize, originalFontFamily, originalForeground, parent);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    ZavershitRedaktirovanieTeksta(textBox, originalText, originalFontSize, originalFontFamily, originalForeground, parent, false);
                    e.Handled = true;
                }
            };

            textBox.LostFocus += (s, e) =>
            {
                ZavershitRedaktirovanieTeksta(textBox, originalText, originalFontSize, originalFontFamily, originalForeground, parent);
            };
        }

        private void FinishTextEditing(TextBox textBox, string originalText, double fontSize, FontFamily fontFamily, Brush foreground, Panel parent, bool saveChanges = true)
        {
            if (textBox == null || parent == null || !parent.Children.Contains(textBox)) return;

            var left = Canvas.GetLeft(textBox);
            var top = Canvas.GetTop(textBox);
            if (double.IsNaN(left)) left = 0;
            if (double.IsNaN(top)) top = 0;

            var newTextBlock = new TextBlock
            {
                Text = saveChanges ? textBox.Text : originalText,
                FontSize = fontSize,
                FontFamily = fontFamily,
                Foreground = foreground
            };

            Canvas.SetLeft(newTextBlock, left);
            Canvas.SetTop(newTextBlock, top);

            int index = parent.Children.IndexOf(textBox);
            parent.Children.RemoveAt(index);
            parent.Children.Insert(index, newTextBlock);

            if (saveChanges)
            {
                MarkDocumentDirty();
            }
        }
    }
}
