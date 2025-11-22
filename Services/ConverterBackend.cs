using FFMpegCore;
using FFMpegCore.Enums;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Formats.Bmp;
using System;
using System.IO;
using System.Threading.Tasks;

namespace UltimateConverter.Services
{
    public static class ConverterBackend
    {
        // === Конвертация Изображений ===
        public static async Task ConvertImageAsync(string inputPath, string outputFolder, string format)
        {
            if (!File.Exists(inputPath)) throw new FileNotFoundException("Файл не найден", inputPath);

            var fileName = Path.GetFileNameWithoutExtension(inputPath);
            var targetExt = format.ToLower();
            // Исправление: корректное расширение для jpeg
            if (targetExt == "jpg") targetExt = "jpeg"; 
            
            var destFile = Path.Combine(outputFolder, $"{fileName}.{targetExt}");

            using var image = await Image.LoadAsync(inputPath);

            IImageEncoder encoder = format.ToLower() switch
            {
                "jpg" or "jpeg" => new JpegEncoder { Quality = 90 },
                "png" => new PngEncoder(),
                "webp" => new WebpEncoder(),
                "bmp" => new BmpEncoder(),
                _ => new JpegEncoder()
            };

            await image.SaveAsync(destFile, encoder);
        }

        // === Конвертация Видео/Аудио ===
        public static async Task ConvertMediaAsync(string inputPath, string outputFolder, string format)
        {
            if (!File.Exists(inputPath)) throw new FileNotFoundException("Файл не найден", inputPath);

            var fileName = Path.GetFileNameWithoutExtension(inputPath);
            // Безопасное извлечение расширения: "MP4 (Video)" -> "mp4"
            var cleanExt = format.Split(' ')[0].ToLower();
            var destFile = Path.Combine(outputFolder, $"{fileName}.{cleanExt}");

            if (cleanExt == "mp3" || cleanExt == "wav")
            {
                // Аудио процессинг
                var processor = FFMpegArguments
                    .FromFileInput(inputPath)
                    .OutputToFile(destFile, true, options => {
                        if (cleanExt == "mp3")
                            options.WithAudioCodec(AudioCodec.LibMp3Lame).ForceFormat("mp3");
                        else
                            options.ForceFormat("wav");
                    });
                
                await processor.ProcessAsynchronously();
            }
            else
            {
                // Видео процессинг
                await FFMpegArguments
                    .FromFileInput(inputPath)
                    .OutputToFile(destFile, true, options => options
                        .WithVideoCodec(VideoCodec.LibX264)
                        .WithAudioCodec(AudioCodec.Aac)
                        .WithSpeedPreset(Speed.VeryFast)) // Оптимизация скорости
                    .ProcessAsynchronously();
            }
        }
    }
}