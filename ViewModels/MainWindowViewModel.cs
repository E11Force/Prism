using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Prism.Services;

namespace Prism.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty] private bool _isImageMode = true;
        [ObservableProperty] private string _statusMessage = "Готов к работе";
        [ObservableProperty] private double _progressValue = 0;
        
        [ObservableProperty] 
        [NotifyPropertyChangedFor(nameof(MainButtonText))] 
        private bool _isBusy = false;
        
        public ObservableCollection<string> ImageFiles { get; } = new();
        public ObservableCollection<string> VideoFiles { get; } = new();

        public string ImageCount => ImageFiles.Count > 0 ? $"{ImageFiles.Count} фото" : "";
        public string VideoCount => VideoFiles.Count > 0 ? $"{VideoFiles.Count} медиа" : "";

        [ObservableProperty] private string _selectedImageFormat = "JPG";
        public List<string> ImageFormats { get; } = new() { "JPG", "PNG", "WEBP", "BMP" };

        [ObservableProperty] private string _selectedVideoFormat = "MP4"; 
        public List<string> VideoFormats { get; } = new() { "MP4", "AVI", "MOV", "MP3", "WAV" };

        [ObservableProperty] private string _outputFolder = "Рядом с исходными файлами";
        private string _realOutputPath = "";

        public string MainButtonText => IsBusy ? "Отменить процесс" : "Начать конвертацию";

        [RelayCommand]
        public async Task MainAction()
        {
            if (IsBusy)
            {
                if (StartConversionCancelCommand.CanExecute(null))
                {
                    StartConversionCancelCommand.Execute(null);
                }
            }
            else
            {
                await StartConversionCommand.ExecuteAsync(null);
            }
        }

        public MainWindowViewModel()
        {
            ImageFiles.CollectionChanged += (s, e) => OnPropertyChanged(nameof(ImageCount));
            VideoFiles.CollectionChanged += (s, e) => OnPropertyChanged(nameof(VideoCount));
        }

        [RelayCommand]
        public void SwitchToImages() { if (!IsBusy) IsImageMode = true; }

        [RelayCommand]
        public void SwitchToVideo() { if (!IsBusy) IsImageMode = false; }

        [RelayCommand]
        public void ClearList()
        {
            if (IsImageMode) ImageFiles.Clear(); else VideoFiles.Clear();
            StatusMessage = "Список очищен";
            ProgressValue = 0;
        }

        public void HandleDroppedFiles(IEnumerable<string> paths)
        {
            var targetList = IsImageMode ? ImageFiles : VideoFiles;
            int added = 0;
            foreach (var path in paths)
            {
                if (File.Exists(path) && !targetList.Contains(path))
                {
                    targetList.Add(path);
                    added++;
                }
            }
            if (added > 0) StatusMessage = $"Перетянуто файлов: {added}";
        }

        [RelayCommand]
        public async Task AddFiles(IStorageProvider storageProvider)
        {
            try 
            {
                var options = new FilePickerOpenOptions
                {
                    Title = IsImageMode ? "Выберите изображения" : "Выберите медиафайлы",
                    AllowMultiple = true,
                    FileTypeFilter = IsImageMode 
                        ? new[] { FilePickerFileTypes.ImageAll }
                        : new[] { new FilePickerFileType("Media") { Patterns = new[] { "*.mp4", "*.avi", "*.mov", "*.mkv", "*.mp3", "*.wav" } }, FilePickerFileTypes.All }
                };

                var files = await storageProvider.OpenFilePickerAsync(options);
                if (files.Count > 0)
                {
                    var targetList = IsImageMode ? ImageFiles : VideoFiles;
                    foreach (var file in files)
                    {
                        if (!targetList.Contains(file.Path.LocalPath))
                            targetList.Add(file.Path.LocalPath);
                    }
                    StatusMessage = $"Добавлено: {files.Count}";
                }
            }
            catch (Exception ex) { StatusMessage = $"Ошибка: {ex.Message}"; }
        }

        [RelayCommand]
        public async Task SelectFolder(IStorageProvider storageProvider)
        {
            try
            {
                var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Куда сохранять?" });
                if (folders.Count > 0)
                {
                    _realOutputPath = folders[0].Path.LocalPath;
                    OutputFolder = _realOutputPath;
                }
            }
            catch { }
        }

        [RelayCommand(IncludeCancelCommand = true)]
        public async Task StartConversion(CancellationToken token)
        {
            var filesToProcess = IsImageMode ? ImageFiles.ToList() : VideoFiles.ToList();
            if (filesToProcess.Count == 0) { StatusMessage = "Список пуст"; return; }

            IsBusy = true;
            ProgressValue = 0;
            int total = filesToProcess.Count;
            int processed = 0;

            try
            {
                await Task.Run(async () =>
                {
                    var parallelOptions = new ParallelOptions 
                    { 
                        MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount - 1),
                        CancellationToken = token 
                    };

                    await Parallel.ForEachAsync(filesToProcess, parallelOptions, async (filePath, ct) =>
                    {
                        try
                        {
                            string outDir = string.IsNullOrEmpty(_realOutputPath) 
                                ? Path.Combine(Path.GetDirectoryName(filePath)!, "Converted") 
                                : _realOutputPath;
                                
                            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

                            if (IsImageMode) 
                                await ConverterBackend.ConvertImageAsync(filePath, outDir, SelectedImageFormat);
                            else 
                                await ConverterBackend.ConvertMediaAsync(filePath, outDir, SelectedVideoFormat);
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}"); }
                        finally
                        {
                            Interlocked.Increment(ref processed);
                            double newProgress = (double)processed / total * 100;
                            await Dispatcher.UIThread.InvokeAsync(() => 
                            {
                                ProgressValue = newProgress;
                                StatusMessage = $"Готово: {processed} из {total}";
                            });
                        }
                    });
                }, token);
                StatusMessage = "Успешно завершено";
                ProgressValue = 100;
            }
            catch (OperationCanceledException) { StatusMessage = "Отменено"; ProgressValue = 0; }
            catch (Exception ex) { StatusMessage = $"Ошибка: {ex.Message}"; }
            finally { IsBusy = false; }
        }
    }
}