using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;
using UltimateConverter.ViewModels;

namespace UltimateConverter.Views
{
    public partial class MainWindow : Window
    {
        // Список элементов, к которым применяем эффект (Контрол, его индивидуальная кисть)
        private readonly List<(Control Control, RadialGradientBrush Brush)> _flashlightTargets = new();

        public MainWindow()
        {
            InitializeComponent();

            // Настройка Drag & Drop
            var imgZone = this.FindControl<Border>("ImgDropZone");
            var vidZone = this.FindControl<Border>("VidDropZone");

            if (imgZone != null) AddDragEvents(imgZone);
            if (vidZone != null) AddDragEvents(vidZone);

            // Подписка на события загрузки, чтобы найти элементы для фонарика
            this.Opened += OnWindowOpened;
            this.PointerMoved += OnPointerMoved;
        }

        private void OnWindowOpened(object? sender, EventArgs e)
        {
            // Находим все элементы, помеченные классом "FlashlightTarget" в XAML
            var targets = this.GetVisualDescendants().OfType<Control>()
                              .Where(c => c.Classes.Contains("FlashlightTarget"));

            foreach (var control in targets)
            {
                // Создаем индивидуальную кисть для каждого элемента
                var brush = new RadialGradientBrush
                {
                    RadiusX = new RelativeScalar(120, RelativeUnit.Absolute),
                    RadiusY = new RelativeScalar(120, RelativeUnit.Absolute),
                    Center = new RelativePoint(-1000, -1000, RelativeUnit.Absolute),
                    GradientOrigin = new RelativePoint(-1000, -1000, RelativeUnit.Absolute)
                };

                // FIX: Яркость
                // Центр пятна (Фонарик) - Идеально белый
                brush.GradientStops.Add(new GradientStop(Color.Parse("#FFFFFFFF"), 0.0));
                
                // Края (Обычное состояние) - "Почти белый" (Светло-серый)
                // Теперь текст отлично читается в спокойном состоянии
                brush.GradientStops.Add(new GradientStop(Color.Parse("#FFC0C0C0"), 1.0)); 

                // Применяем кисть
                if (control is TextBlock textBlock)
                    textBlock.Foreground = brush;
                else if (control is PathIcon pathIcon)
                    pathIcon.Foreground = brush;
                else if (control is Path path)
                    path.Fill = brush;
                else if (control is ContentPresenter cp)
                    cp.Foreground = brush;
                else if (control is Button button)
                    button.Foreground = brush;

                _flashlightTargets.Add((control, brush));
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            foreach (var (control, brush) in _flashlightTargets)
            {
                var relativePoint = e.GetPosition(control);

                brush.Center = new RelativePoint(relativePoint.X, relativePoint.Y, RelativeUnit.Absolute);
                brush.GradientOrigin = new RelativePoint(relativePoint.X, relativePoint.Y, RelativeUnit.Absolute);
            }
        }

        private void AddDragEvents(Border border)
        {
            border.AddHandler(DragDrop.DragOverEvent, DragOver);
            border.AddHandler(DragDrop.DropEvent, Drop);
        }

        private void DragOver(object? sender, DragEventArgs e)
        {
            #pragma warning disable CS0618
            var files = e.Data.GetFiles(); 
            #pragma warning restore CS0618

            e.DragEffects = (files != null && files.Any()) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void Drop(object? sender, DragEventArgs e)
        {
            #pragma warning disable CS0618
            var files = e.Data.GetFiles();
            #pragma warning restore CS0618
            
            if (DataContext is MainWindowViewModel vm && files != null)
            {
                var paths = files.Select(f => f.Path.LocalPath);
                vm.HandleDroppedFiles(paths);
            }
        }
    }
}