using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Platform.Storage;
using Avalonia.Controls.Shapes;
using Path = Avalonia.Controls.Shapes.Path;

namespace Schem;

// Кастомизированный фон (обои)
public class WaveBackground : Control
{
    private double _time = 0;
    private Point _mousePos = new Point(-1000, -1000);
    private List<Point> _blockCenters = new();
    private readonly DispatcherTimer _timer;

    public WaveBackground()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (s, e) =>
        {
            _time += 0.04;
            InvalidateVisual();
        };
        _timer.Start();
    }

    public void UpdateMouse(Point pos) => _mousePos = pos;
    public void UpdateBlocks(List<Point> blocks) => _blockCenters = blocks;

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(Brushes.Black, Bounds);

        var pen = new Pen(new SolidColorBrush(Color.FromArgb(140, 255, 255, 255)), 1.5);
        
        double width = Bounds.Width;
        double height = Bounds.Height;
        if (width <= 0 || height <= 0) return;

        int linesCount = 35;
        int pointsPerLine = 60;
        
        double spacingY = (height + 80) / linesCount;
        double spacingX = width / pointsPerLine;

        for (int i = 0; i < linesCount; i++)
        {
            var path = new StreamGeometry();
            using var ctx = path.Open();

            double baseY = i * spacingY - 20;
            bool isFirst = true;

            for (int j = 0; j <= pointsPerLine; j++)
            {
                double x = j * spacingX;

                double projectedY = baseY - (height - baseY) * 0.1;
                double z = Math.Sin((x * 0.02) + _time) * 12 + Math.Cos((baseY * 0.02) + _time) * 12;

                double screenY = projectedY + z;

                double distToMouse = GetDistance(x, screenY, _mousePos.X, _mousePos.Y);
                if (distToMouse < 140)
                {
                    z += (140 - distToMouse) * Math.Sin(_time * 2) * 0.25;
                }

                foreach (var block in _blockCenters)
                {
                    double distToBlock = GetDistance(x, screenY, block.X, block.Y);
                    if (distToBlock < 110)
                    {
                        z -= (110 - distToBlock) * 0.35;
                    }
                }

                double finalY = projectedY + z;

                if (isFirst)
                {
                    ctx.BeginFigure(new Point(x, finalY), false);
                    isFirst = false;
                }
                else
                {
                    ctx.LineTo(new Point(x, finalY));
                }
            }
            context.DrawGeometry(Brushes.Transparent, pen, path);
        }
    }

    private double GetDistance(double x1, double y1, double x2, double y2)
    {
        return Math.Sqrt((x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1));
    }
}

// Сериализации и модели данных
public class NodeModel
{
    public string Id { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Text { get; set; } = "";
    public string ColorName { get; set; } = "";
}

public class LinkModel
{
    public string ParentId { get; set; } = "";
    public string ChildId { get; set; } = "";
}

public class DiagramState
{
    public int ModeIndex { get; set; } = 0;
    public double WindowWidth { get; set; } = 0;
    public double WindowHeight { get; set; } = 0;
    public double PanX { get; set; } = 0; // Смещение холста по X
    public double PanY { get; set; } = 0; // Смещение холста по Y
    public List<NodeModel> Nodes { get; set; } = new();
    public List<LinkModel> Links { get; set; } = new();
}

public class DiagramItemData
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ColorName { get; set; } = "Черный";
}

public class DiagramLink
{
    public Border Parent { get; set; } = null!;
    public Border Child { get; set; } = null!;
    public Path OutlineLine { get; set; } = null!;
    public Path MainLine { get; set; } = null!;
}

// Логика окна
public partial class MainWindow : Window
{
    // Переменные для отслеживания жестов мыши и текста
    private string _gestureStartState = "";
    private bool _hasGestureMoved = false;
    private bool _isResizing = false;
    private bool _isDragging = false;
    private string _textBeforeEdit = "";
    //

    private Border? _selectedElement;
    private Border? _clipboardSource;
    private bool _clipboardAsChild;
    private int _elementCounter = 0;
    
    private readonly List<DiagramLink> _links = new();
    
    // Система истории для Ctrl+Z
    private readonly Stack<string> _undoHistory = new();
    private bool _isRestoring = false;

// Переменные панорамирования (сдвига холста)
    private double _panX = 0;
    private double _panY = 0;
    private bool _isPanning = false;
    private Point _panStartMouse;
    private Point _panStartOffset;

    private double _zoomScale = 1.0;
    private readonly ScaleTransform _scaleTransform = new ScaleTransform(1.0, 1.0);
    private readonly TranslateTransform _panTransform = new TranslateTransform(0, 0);
    private readonly TransformGroup _transformGroup = new TransformGroup();

    public MainWindow()
    {
        InitializeComponent();
        
        // Собираем группу: сначала масштабирование, затем смещение
        _transformGroup.Children.Add(_scaleTransform);
        _transformGroup.Children.Add(_panTransform);
        DiagramLayer.RenderTransform = _transformGroup;
    }

    private TaskCompletionSource<bool>? _warningTcs;

    private void OnHelpClick(object? sender, RoutedEventArgs e) => HelpOverlay.IsVisible = true;
    private void OnCloseHelpClick(object? sender, RoutedEventArgs e) => HelpOverlay.IsVisible = false;

    private Task<bool> ShowWarningAsync(string message)
    {
        WarningText.Text = message;
        WarningOverlay.IsVisible = true;
        _warningTcs = new TaskCompletionSource<bool>();
        return _warningTcs.Task;
    }

    private void OnWarningYesClick(object? sender, RoutedEventArgs e)
    {
        WarningOverlay.IsVisible = false;
        _warningTcs?.TrySetResult(true);
    }

    private void OnWarningNoClick(object? sender, RoutedEventArgs e)
    {
        WarningOverlay.IsVisible = false;
        _warningTcs?.TrySetResult(false);
    }

    // Системы отмены
    private void CaptureState()
    {
        if (_isRestoring) return;
        _undoHistory.Push(ExportToJson());
    }

    private async void Undo()
    {
        if (_undoHistory.Count > 0)
        {
            _isRestoring = true;
            string state = _undoHistory.Pop();
            await ImportFromJson(state, skipWarning: true);
            _isRestoring = false;
        }
    }

    // Обработка хоткеев
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Source is TextBox) return;

        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        if (ctrl && e.Key == Key.Z)
        {
            Undo();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.S)
        {
            _ = SaveFileAsync();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.O)
        {
            _ = LoadFileAsync();
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.X)
        {
            // Переключение режимов (Схема/Древо)
            if (shift)
            {
                ModePicker.SelectedIndex = ModePicker.SelectedIndex > 0 ? ModePicker.SelectedIndex - 1 : ModePicker.Items.Count - 1;
            }
            else
            {
                ModePicker.SelectedIndex = ModePicker.SelectedIndex < ModePicker.Items.Count - 1 ? ModePicker.SelectedIndex + 1 : 0;
            }
            e.Handled = true;
        }
        else if (ctrl && e.Key == Key.C)
        {
            if (_selectedElement != null)
            {
                _clipboardSource = _selectedElement;
                _clipboardAsChild = shift; 
            }
        }
        else if (ctrl && e.Key == Key.V)
        {
            if (_clipboardSource != null)
            {
                CaptureState();
                bool asChild = shift || _clipboardAsChild;
                CopyNode(_clipboardSource, asChild);
            }
        }
    }

    private void UpdateBackgroundObstacles()
    {
        var centers = new List<Point>();
        foreach (var child in DiagramLayer.Children.OfType<Border>())
        {
            if (child.Child is Grid)
            {
                double cx = (Canvas.GetLeft(child) + child.Width / 2) * _zoomScale + _panX;
                double cy = (Canvas.GetTop(child) + child.Height / 2) * _zoomScale + _panY;
                centers.Add(new Point(cx, cy));
            }
        }
        InteractiveBg.UpdateBlocks(centers);
    }

    public void CenterDiagram()
    {
        var borders = DiagramLayer.Children.OfType<Border>().Where(b => b.Tag is DiagramItemData).ToList();
        if (borders.Count == 0) return;

        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;

        foreach (var b in borders)
        {
            double x = Canvas.GetLeft(b);
            double y = Canvas.GetTop(b);
            double w = b.Width;
            double h = b.Height;

            if (x < minX) minX = x;
            if (x + w > maxX) maxX = x + w;
            if (y < minY) minY = y;
            if (y + h > maxY) maxY = y + h;
        }

        double diagramCenterX = minX + (maxX - minX) / 2.0;
        double diagramCenterY = minY + (maxY - minY) / 2.0;

        double windowCenterX = (MainCanvas.Bounds.Width > 0 ? MainCanvas.Bounds.Width : this.Bounds.Width) / 2.0;
        double windowCenterY = (MainCanvas.Bounds.Height > 0 ? MainCanvas.Bounds.Height : this.Bounds.Height) / 2.0;

        _panX = windowCenterX - diagramCenterX;
        _panY = windowCenterY - diagramCenterY;

        _panTransform.X = _panX;
        _panTransform.Y = _panY;

        UpdateBackgroundObstacles();
    }

    private void OnCenterClick(object? sender, RoutedEventArgs e)
    {
        CenterDiagram();
    }

    private void OnCanvasPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            // Определяем направление прокрутки (1.1 — приближение, 0.9 — отдаление)
            double zoomFactor = e.Delta.Y > 0 ? 1.1 : 0.9;
            double newScale = Math.Clamp(_zoomScale * zoomFactor, 0.2, 4.0); // Ограничение масштаба (от 20% до 400%)

            if (Math.Abs(newScale - _zoomScale) > 0.001)
            {
                // Позиция курсора относительно окна, чтобы зумить ровно в точку под мышью
                Point mousePos = e.GetPosition(MainCanvas);

                _panX = mousePos.X - (mousePos.X - _panX) * (newScale / _zoomScale);
                _panY = mousePos.Y - (mousePos.Y - _panY) * (newScale / _zoomScale);

                _zoomScale = newScale;
                
                _scaleTransform.ScaleX = _zoomScale;
                _scaleTransform.ScaleY = _zoomScale;

                _panTransform.X = _panX;
                _panTransform.Y = _panY;

                UpdateBackgroundObstacles();
            }
            e.Handled = true;
        }
    }
    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        InteractiveBg.UpdateMouse(e.GetPosition(this));

        if (_isPanning)
        {
            var currentMouse = e.GetPosition(this);
            var delta = currentMouse - _panStartMouse;
            _panX = _panStartOffset.X + delta.X;
            _panY = _panStartOffset.Y + delta.Y;

            _panTransform.X = _panX;
            _panTransform.Y = _panY;

            UpdateBackgroundObstacles();
            e.Handled = true;
        }
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void OnModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateAllLinks();
    }

    private Point GetFreePosition(double startX, double startY, double width, double height)
    {
        double x = startX, y = startY, spacing = 15; 
        bool overlaps = true;
        
        while (overlaps)
        {
            overlaps = false;
            var newRect = new Rect(x, y, width, height);

            // Проверяем пересечения на слое DiagramLayer
            foreach (var child in DiagramLayer.Children.OfType<Border>())
            {
                if (child.Tag is DiagramItemData)
                {
                    var childRect = new Rect(Canvas.GetLeft(child), Canvas.GetTop(child), child.Width, child.Height);
                    if (newRect.Intersects(childRect))
                    {
                        // Если перекрывает — сдвигаем новый блок чуть ниже
                        y = Canvas.GetTop(child) + child.Height + spacing;
                        overlaps = true;
                        break;
                    }
                }
            }
        }
        return new Point(x, y);
    }

    private bool CanMoveTo(Border movingNode, double newX, double newY)
    {
        var newRect = new Rect(newX, newY, movingNode.Width, movingNode.Height);
        foreach (var other in DiagramLayer.Children.OfType<Border>())
        {
            if (other == movingNode) continue;
            var otherRect = new Rect(Canvas.GetLeft(other), Canvas.GetTop(other), other.Width, other.Height);
            if (newRect.Intersects(otherRect)) return false; 
        }
        return true;
    }

    private void OnAddRootClick(object? sender, RoutedEventArgs e)
    {
        CaptureState();
        _elementCounter++;
        string colorName = (ColorPicker.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Черный";
        double w = WidthSpinner.Value.HasValue ? (double)WidthSpinner.Value.Value : 140;
        double h = HeightSpinner.Value.HasValue ? (double)HeightSpinner.Value.Value : 60;

        // Определяем видимый левый верхний угол с учетом сдвига и зума
        double spawnX = (-_panX + 50) / _zoomScale;
        double spawnY = (-_panY + 50) / _zoomScale;

        var pos = GetFreePosition(spawnX, spawnY, w, h);
        CreateDiagramNode(pos.X, pos.Y, w, h, $"Корневой {_elementCounter}", colorName);
    }

    private Border CreateDiagramNode(double x, double y, double w, double h, string text, string colorName, string forceId = "")
    {
        bool isBlack = colorName == "Черный";
        IBrush bgBrush = isBlack ? Brushes.Black : Brushes.White;
        IBrush fgBrush = isBlack ? Brushes.White : Brushes.Black;
        IBrush defaultBorder = isBlack ? Brushes.White : Brushes.Black;

        string id = string.IsNullOrEmpty(forceId) ? Guid.NewGuid().ToString() : forceId;

        var container = new Border
        {
            Width = w, Height = h,
            Background = bgBrush,
            CornerRadius = new CornerRadius(5),
            BorderBrush = defaultBorder,
            BorderThickness = new Avalonia.Thickness(2),
            Tag = new DiagramItemData { Id = id, ColorName = colorName }
        };

        var grid = new Grid();

        var textBox = new TextBox
        {
            Text = text,
            Foreground = fgBrush,
            CaretBrush = fgBrush,
            Background = Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            TextAlignment = TextAlignment.Center,
            AcceptsReturn = true,
            Margin = new Avalonia.Thickness(5)
        };

        // Запоминаем текст при начале редактирования и сохраняем историю только при завершении ввода
        textBox.GotFocus += (s, e) => { _textBeforeEdit = textBox.Text ?? ""; };
        textBox.LostFocus += (s, e) =>
        {
            if (textBox.Text != _textBeforeEdit)
            {
                CaptureState();
            }
        };

        grid.Children.Add(textBox);
        container.Child = grid;

        // Контекстное меню
        var contextMenu = new ContextMenu();
        var addSubItem = new MenuItem { Header = "Добавить пустой дочерний" };
        addSubItem.Click += (s, e) => { CaptureState(); AddChildNode(container); };
        var copyRootItem = new MenuItem { Header = "Копировать блок (отдельный)" };
        copyRootItem.Click += (s, e) => { CaptureState(); CopyNode(container, false); };
        var copyChildItem = new MenuItem { Header = "Копировать как дочерний" };
        copyChildItem.Click += (s, e) => { CaptureState(); CopyNode(container, true); };
        var deleteItem = new MenuItem { Header = "Удалить элемент", Foreground = Brushes.Red };
        deleteItem.Click += (s, e) => { CaptureState(); DeleteNode(container); };
        
        contextMenu.ItemsSource = new[] { addSubItem, copyRootItem, copyChildItem, deleteItem };
        container.ContextMenu = contextMenu;

        Point actionStartPos = new Point();
        double startW = 0, startH = 0;
        double startOffsetX = 0, startOffsetY = 0;

        // --- НАЧАЛО ЖЕСТА (НАЖАТИЕ МЫШИ) ---
        container.PointerPressed += (s, e) =>
        {
            if (!e.Handled && e.GetCurrentPoint(MainCanvas).Properties.IsLeftButtonPressed)
            {
                SelectElement(container);
                
                _gestureStartState = ExportToJson();
                _hasGestureMoved = false;

                if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
                {
                    _isResizing = true;
                    _isDragging = false;
                    // Считываем стартовую точку растяжения в координатах виртуального слоя DiagramLayer
                    actionStartPos = e.GetPosition(DiagramLayer);
                    startW = container.Width;
                    startH = container.Height;
                }
                else
                {
                    _isDragging = true;
                    _isResizing = false;
                    var pos = e.GetPosition(container);
                    startOffsetX = pos.X;
                    startOffsetY = pos.Y;
                }
                
                e.Pointer.Capture(container);
                e.Handled = true;
            }
        };

        // --- ДВИЖЕНИЕ МЫШИ ПРИ ПЕРЕМЕЩЕНИИ / ИЗМЕНЕНИИ РАЗМЕРА ---
        container.PointerMoved += (s, e) =>
        {
            if (container.Parent is Canvas)
            {
                if (_isResizing)
                {
                    // Считываем текущую позицию мыши на слое DiagramLayer (учитывает зум и сдвиг)
                    var pos = e.GetPosition(DiagramLayer);
                    double newWidth = startW + (pos.X - actionStartPos.X);
                    double newHeight = startH + (pos.Y - actionStartPos.Y);

                    if (newWidth > 60) container.Width = newWidth;
                    if (newHeight > 40) container.Height = newHeight;

                    WidthSpinner.Value = (decimal)container.Width;
                    HeightSpinner.Value = (decimal)container.Height;
                    
                    _hasGestureMoved = true;
                    UpdateAllLinks();
                    UpdateBackgroundObstacles();
                    e.Handled = true;
                }
                else if (_isDragging)
                {
                    var pos = e.GetPosition(DiagramLayer);
                    double newX = pos.X - startOffsetX;
                    double newY = pos.Y - startOffsetY;

                    if (CanMoveTo(container, newX, newY))
                    {
                        Canvas.SetLeft(container, newX);
                        Canvas.SetTop(container, newY);
                        
                        _hasGestureMoved = true;
                        UpdateAllLinks();
                        UpdateBackgroundObstacles();
                    }
                    e.Handled = true;
                }
            }
        };

        // --- ЗАВЕРШЕНИЕ ЖЕСТА (ОТПУСКАНИЕ МЫШИ) ---
        container.PointerReleased += (s, e) =>
        {
            // Записываем в стек истории ТОЛЬКО если элемент реально сдвинулся или изменился в размере
            if (_hasGestureMoved && !string.IsNullOrEmpty(_gestureStartState))
            {
                _undoHistory.Push(_gestureStartState);
            }

            _isResizing = false;
            _isDragging = false;
            _hasGestureMoved = false;
            _gestureStartState = "";

            e.Pointer.Capture(null);
            e.Handled = true;
        };

        Canvas.SetLeft(container, x);
        Canvas.SetTop(container, y);
        DiagramLayer.Children.Add(container); // Было MainCanvas.Children.Add
        SelectElement(container);
        UpdateBackgroundObstacles();

        return container;
    }

    private void AddChildNode(Border parent)
    {
        _elementCounter++;
        bool isTreeMode = ModePicker.SelectedIndex == 1;
        
        double startX, startY;

        if (isTreeMode)
        {
            startX = Canvas.GetLeft(parent) + (parent.Width / 2) - 70;
            startY = Canvas.GetTop(parent) + parent.Height + 60;
        }
        else
        {
            startX = Canvas.GetLeft(parent) + parent.Width + 60;
            startY = Canvas.GetTop(parent);
        }
        
        var freePos = GetFreePosition(startX, startY, 140, 60);
        string colorName = (ColorPicker.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Черный";
        var child = CreateDiagramNode(freePos.X, freePos.Y, 140, 60, $"Узел {_elementCounter}", colorName);

        CreateLink(parent, child);
    }

    private void CopyNode(Border sourceNode, bool asChild)
    {
        double startX = Canvas.GetLeft(sourceNode) + sourceNode.Width + 40;
        double startY = Canvas.GetTop(sourceNode);
        var freePos = GetFreePosition(startX, startY, sourceNode.Width, sourceNode.Height);
        var meta = (DiagramItemData)sourceNode.Tag!;
        
        string text = "";
        if (sourceNode.Child is Grid g && g.Children.FirstOrDefault(c => c is TextBox) is TextBox t)
        {
            text = t.Text ?? "";
        }
        
        var newNode = CreateDiagramNode(freePos.X, freePos.Y, sourceNode.Width, sourceNode.Height, text, meta.ColorName);
        if (asChild) CreateLink(sourceNode, newNode);
    }

    private void CreateLink(Border parent, Border child)
    {
        var outlinePath = new Path { Stroke = Brushes.Black, StrokeThickness = 4, ZIndex = -2 };
        var mainPath = new Path { Stroke = Brushes.White, StrokeThickness = 2, ZIndex = -1 };

        DiagramLayer.Children.Add(outlinePath); // Было MainCanvas.Children
        DiagramLayer.Children.Add(mainPath);

        _links.Add(new DiagramLink { Parent = parent, Child = child, OutlineLine = outlinePath, MainLine = mainPath });
        UpdateAllLinks();
    }

    private string I(double value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private void UpdateAllLinks()
    {
        bool isTreeMode = ModePicker?.SelectedIndex == 1;

        foreach (var link in _links)
        {
            double pX, pY, cX, cY;
            string pathData;

            if (isTreeMode)
            {
                pX = Canvas.GetLeft(link.Parent) + link.Parent.Width / 2;
                pY = Canvas.GetTop(link.Parent) + link.Parent.Height;
                cX = Canvas.GetLeft(link.Child) + link.Child.Width / 2;
                cY = Canvas.GetTop(link.Child);
                pathData = $"M {I(pX)},{I(pY)} C {I(pX)},{I(pY + 50)} {I(cX)},{I(cY - 50)} {I(cX)},{I(cY)}";
            }
            else
            {
                pX = Canvas.GetLeft(link.Parent) + link.Parent.Width;
                pY = Canvas.GetTop(link.Parent) + link.Parent.Height / 2;
                cX = Canvas.GetLeft(link.Child);
                cY = Canvas.GetTop(link.Child) + link.Child.Height / 2;
                pathData = $"M {I(pX)},{I(pY)} C {I(pX + 50)},{I(pY)} {I(cX - 50)},{I(cY)} {I(cX)},{I(cY)}";
            }
            
            var geometry = Geometry.Parse(pathData);
            link.OutlineLine.Data = geometry;
            link.MainLine.Data = geometry;
        }
    }

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Если кликнули по пустому полю холста
        if (e.Source == MainCanvas || e.Source is Canvas) 
        {
            DeselectElement();
            MainCanvas.Focusable = true;
            MainCanvas.Focus();

            // Запускаем перетаскивание схемы при зажатии ЛКМ
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isPanning = true;
                _panStartMouse = e.GetPosition(this);
                _panStartOffset = new Point(_panX, _panY);
                e.Pointer.Capture(MainCanvas);
                e.Handled = true;
            }
        }
    }

    private void DeselectElement()
    {
        if (_selectedElement != null && _selectedElement.Tag is DiagramItemData oldMeta)
        {
            bool isOldBlack = oldMeta.ColorName == "Черный";
            _selectedElement.BorderBrush = isOldBlack ? Brushes.White : Brushes.Black;
            _selectedElement = null;
        }
    }

    private void SelectElement(Border elem)
    {
        DeselectElement();
        _selectedElement = elem;
        _selectedElement.BorderBrush = Brushes.DeepSkyBlue; 
        WidthSpinner.Value = (decimal)_selectedElement.Width;
        HeightSpinner.Value = (decimal)_selectedElement.Height;
    }

    private void OnSizeChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        // Не записываем историю, если изменение размера происходит во время drag/resize мышыо
        if (_isDragging || _isResizing) return;

        if (_selectedElement != null && e.NewValue.HasValue)
        {
            CaptureState();
            if (sender == WidthSpinner) _selectedElement.Width = (double)e.NewValue.Value;
            if (sender == HeightSpinner) _selectedElement.Height = (double)e.NewValue.Value;
            UpdateAllLinks();
            UpdateBackgroundObstacles();
        }
    }

    private void DeleteNode(Border node)
    {
        var linksToRemove = _links.Where(l => l.Parent == node || l.Child == node).ToList();
        foreach (var link in linksToRemove)
        {
            DiagramLayer.Children.Remove(link.OutlineLine); // Было MainCanvas.Children
            DiagramLayer.Children.Remove(link.MainLine);
            _links.Remove(link);
        }
        DiagramLayer.Children.Remove(node);
        if (_selectedElement == node) _selectedElement = null;
        UpdateBackgroundObstacles();
    }

    private string ExportToJson()
    {
        var state = new DiagramState
        {
            ModeIndex = ModePicker.SelectedIndex,
            WindowWidth = this.Bounds.Width,
            WindowHeight = this.Bounds.Height,
            PanX = _panX,
            PanY = _panY
        };
        
        foreach (var child in DiagramLayer.Children.OfType<Border>())
        {
            if (child.Tag is DiagramItemData meta)
            {
                state.Nodes.Add(new NodeModel 
                { 
                    Id = meta.Id, X = Canvas.GetLeft(child), Y = Canvas.GetTop(child), 
                    Width = child.Width, Height = child.Height, 
                    Text = (child.Child as Grid)?.Children.OfType<TextBox>().FirstOrDefault()?.Text ?? "", 
                    ColorName = meta.ColorName 
                });
            }
        }

        foreach (var link in _links) 
        {
            state.Links.Add(new LinkModel { ParentId = ((DiagramItemData)link.Parent.Tag!).Id, ChildId = ((DiagramItemData)link.Child.Tag!).Id });
        }

        return JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
    }

    private async Task<bool> ImportFromJson(string json, bool skipWarning = false)
    {
        try
        {
            var state = JsonSerializer.Deserialize<DiagramState>(json);
            if (state == null) return false;

            if (!skipWarning && state.WindowWidth > 0 && state.WindowHeight > 0)
            {
                double curW = this.Bounds.Width, curH = this.Bounds.Height;
                if (Math.Abs(state.WindowWidth - curW) > 5 || Math.Abs(state.WindowHeight - curH) > 5)
                {
                    string msg = $"Файл был создан в разрешении {state.WindowWidth}x{state.WindowHeight},\n" +
                                 $"а текущее окно {curW}x{curH}.\n\n" +
                                 $"Вы уверены что хотите его открыть? Элементы могут оказаться за пределами экрана.";
                    bool proceed = await ShowWarningAsync(msg);
                    if (!proceed) return false;
                }
            }

            var borders = DiagramLayer.Children.OfType<Border>().ToList();
            foreach (var b in borders) DiagramLayer.Children.Remove(b);
            var paths = DiagramLayer.Children.OfType<Path>().ToList();
            foreach (var p in paths) DiagramLayer.Children.Remove(p);
            _links.Clear(); 
            _selectedElement = null;

            ModePicker.SelectedIndex = state.ModeIndex;

            var idToBorder = new Dictionary<string, Border>();
            foreach (var n in state.Nodes) 
            {
                idToBorder[n.Id] = CreateDiagramNode(n.X, n.Y, n.Width, n.Height, n.Text, n.ColorName, n.Id);
            }

            foreach (var l in state.Links) 
            {
                if (idToBorder.TryGetValue(l.ParentId, out var p) && idToBorder.TryGetValue(l.ChildId, out var c)) 
                {
                    CreateLink(p, c);
                }
            }
            
            CenterDiagram();

            return true;
        }
        catch 
        { 
            return false; 
        }
    }

    private async Task SaveFileAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Сохранить схему",
            DefaultExtension = "json",
            FileTypeChoices = new[] { new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } } }
        });

        if (file != null)
        {
            await File.WriteAllTextAsync(file.Path.LocalPath, ExportToJson());
        }
    }

    private async Task LoadFileAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;
        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Открыть схему", AllowMultiple = false, FileTypeFilter = new[] { new FilePickerFileType("JSON Files") { Patterns = new[] { "*.json" } } } });
        if (files.Count > 0)
        {
            string json = await File.ReadAllTextAsync(files[0].Path.LocalPath);
            CaptureState();
            bool success = await ImportFromJson(json);
            if (!success) _undoHistory.Pop(); // Если отменили открытие - удаляем пустой шаг истории
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => _ = SaveFileAsync();
    private void OnLoadClick(object? sender, RoutedEventArgs e) => _ = LoadFileAsync();
    
    private void OnClearClick(object? sender, RoutedEventArgs? e)
    {
        CaptureState();
        var borders = DiagramLayer.Children.OfType<Border>().ToList();
        foreach (var b in borders) DiagramLayer.Children.Remove(b);
        var paths = DiagramLayer.Children.OfType<Path>().ToList();
        foreach (var p in paths) DiagramLayer.Children.Remove(p);
        _links.Clear();
        _selectedElement = null;

        _panX = 0;
        _panY = 0;
        _panTransform.X = 0;
        _panTransform.Y = 0;

        UpdateBackgroundObstacles();
    }
}