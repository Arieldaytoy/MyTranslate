using AlibabaCloud.OpenApiClient.Models;
using AlibabaCloud.SDK.Ocr_api20210707;
using AlibabaCloud.SDK.Ocr_api20210707.Models;
using AlibabaCloud.TeaUtil.Models;
using System.Drawing.Imaging;
using System.Text.Json;

namespace MyTranslate.Capture
{
    /// <summary>
    /// 阿里云 OCR 提供程序
    /// 支持两种识别方式：
    /// 1. 通过公网图片 URL
    /// 2. 通过 Bitmap 对象（通过 body 二进制流上传）
    /// </summary>
    public class AlibabaOcrProvider : IOcrProvider
    {
        public const string AlibabaId = "alibaba_ocr";
        private readonly Client _client;

        public string Name => "阿里 OCR";
        public string Id => AlibabaId;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="accessKeyId">阿里云 AccessKey ID</param>
        /// <param name="accessKeySecret">阿里云 AccessKey Secret</param>
        public AlibabaOcrProvider(string accessKeyId, string accessKeySecret)
        {
            if (string.IsNullOrEmpty(accessKeyId) || string.IsNullOrEmpty(accessKeySecret))
                throw new ArgumentException("AccessKey 不能为空");

            var config = new Config
            {
                AccessKeyId = accessKeyId,
                AccessKeySecret = accessKeySecret,
                Endpoint = "ocr-api.cn-hangzhou.aliyuncs.com"
            };
            _client = new Client(config);
        }

        ///// <summary>
        ///// 通过公网可访问的图片 URL 进行 OCR 识别
        ///// </summary>
        ///// <param name="imageUrl">图片的完整 URL（例如 https://example.com/image.jpg）</param>
        ///// <returns>识别出的文字内容</returns>
        //public async Task<string> RecognizeAsync(string imageUrl)
        //{
        //    if (string.IsNullOrWhiteSpace(imageUrl))
        //        throw new ArgumentException("图片 URL 不能为空");

        //    var request = new RecognizeGeneralRequest
        //    {
        //        Url = imageUrl
        //    };
        //    var runtime = new RuntimeOptions();

        //    try
        //    {
        //        var response = await _client.RecognizeGeneralWithOptionsAsync(request, runtime);

        //        System.Diagnostics.Debug.WriteLine($"[OCR URL] Response: {JsonSerializer.Serialize(response)}");

        //        if (response?.Body?.Code != null && response.Body.Code != "200")
        //            throw new Exception($"OCR 错误: {response.Body.Code} - {response.Body.Message}");

        //        if (response?.Body?.Data != null)
        //        {
        //            using var doc = JsonDocument.Parse(response.Body.Data);
        //            if (doc.RootElement.TryGetProperty("content", out var content))
        //                return content.GetString() ?? "";
        //            return response.Body.Data.ToString();
        //        }
        //        return string.Empty;
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new Exception($"OCR 识别失败: {ex.Message}", ex);
        //    }
        //}

        /// <summary>
        /// 通过 Bitmap 对象进行 OCR 识别（自动缩放、压缩后通过 body 二进制流上传）
        /// </summary>
        /// <param name="image">要识别的 Bitmap 图像</param>
        /// <returns>识别出的文字内容</returns>
        public async Task<string> RecognizeAsync(Bitmap image)
        {
            // 限制输出图片的最大尺寸
            const int maxWidth = 1200;
            const int maxHeight = 1200;
            using var resizedImage = ResizeImage(image, maxWidth, maxHeight);

            // 保存到临时文件，再通过文件流上传（与阿里云官方示例一致）
            var tempDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "temp");
            Directory.CreateDirectory(tempDir);
            var tempFile = Path.Combine(tempDir, $"ocr_{Guid.NewGuid():N}.jpg");
            try
            {
                // 保存为 JPEG 格式，质量 80
                var jpegCodec = GetEncoder(ImageFormat.Jpeg);
                if (jpegCodec != null)
                {
                    var encoderParams = new EncoderParameters(1);
                    encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, 80L);
                    resizedImage.Save(tempFile, jpegCodec, encoderParams);
                }
                else
                {
                    resizedImage.Save(tempFile, ImageFormat.Png);
                }

                // 通过文件流读取图片（与阿里云官方示例 StreamUtil.ReadFromFilePath 一致）
                var bodyStream = AlibabaCloud.DarabonbaStream.StreamUtil.ReadFromFilePath(tempFile);
                var request = new RecognizeGeneralRequest
                {
                    Body = bodyStream,
                };
                var runtime = new RuntimeOptions();

                var response = await _client.RecognizeGeneralWithOptionsAsync(request, runtime);
                System.Diagnostics.Debug.WriteLine($"[OCR Bitmap] Response: {JsonSerializer.Serialize(response)}");

                if (response?.Body?.Code != null && response.Body.Code != "200")
                    throw new Exception($"OCR 错误: {response.Body.Code} - {response.Body.Message}");

                if (response?.Body?.Data != null)
                {
                    using var doc = JsonDocument.Parse(response.Body.Data);
                    if (doc.RootElement.TryGetProperty("content", out var content))
                        return content.GetString() ?? "";
                    return response.Body.Data.ToString();
                }
                return string.Empty;
            }
            catch (Exception ex)
            {
                throw new Exception($"OCR 识别失败: {ex.Message}", ex);
            }
            finally
            {
                // 清理临时文件
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            }
        }

        public bool IsConfigured() => true;

        #region 辅助方法

        /// <summary>
        /// 缩放图片，保持宽高比，限制最大尺寸
        /// </summary>
        private static Bitmap ResizeImage(Image original, int maxWidth, int maxHeight)
        {
            double ratioX = (double)maxWidth / original.Width;
            double ratioY = (double)maxHeight / original.Height;
            double ratio = Math.Min(ratioX, ratioY);
            if (ratio >= 1.0)
                return new Bitmap(original);

            int newWidth = (int)(original.Width * ratio);
            int newHeight = (int)(original.Height * ratio);
            var newImage = new Bitmap(newWidth, newHeight);
            using var g = Graphics.FromImage(newImage);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(original, 0, 0, newWidth, newHeight);
            return newImage;
        }

        /// <summary>
        /// 获取指定图像格式的编码器
        /// </summary>
        private static ImageCodecInfo? GetEncoder(ImageFormat format)
        {
            var codecs = ImageCodecInfo.GetImageEncoders();
            foreach (var codec in codecs)
                if (codec.FormatID == format.Guid)
                    return codec;
            return null;
        }

        #endregion
    }
}
