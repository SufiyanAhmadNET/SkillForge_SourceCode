using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using SkillForge.Interfaces;
using System.IO;

namespace SkillForge.Services.Common
{
    public class MediaService : IMediaService
    {
        private readonly Cloudinary _cloudinary;

        public MediaService(Cloudinary cloudinary)
        {
            _cloudinary = cloudinary;
        }

        // Helper to upload file to Cloudinary
        private string? UploadToCloudinary(IFormFile file, string folder, string resourceType = "image")
        {
            if (file == null || file.Length == 0) return null;

            using var stream = file.OpenReadStream();
            var fileDesc = new FileDescription(file.FileName, stream);
            
            if (resourceType == "video")
            {
                var uploadParams = new VideoUploadParams()
                {
                    File = fileDesc,
                    Folder = $"skillforge/{folder}",
                    Overwrite = true
                };
                var result = _cloudinary.Upload(uploadParams);
                if (result.Error != null) throw new Exception($"Cloudinary Video Error: {result.Error.Message}");
                return result.SecureUrl.ToString();
            }
            else if (resourceType == "raw")
            {
                var uploadParams = new RawUploadParams()
                {
                    File = fileDesc,
                    Folder = $"skillforge/{folder}"
                };
                var result = _cloudinary.Upload(uploadParams);
                if (result.Error != null) throw new Exception($"Cloudinary Raw Error: {result.Error.Message}");
                return result.SecureUrl.ToString();
            }
            else
            {
                var uploadParams = new ImageUploadParams()
                {
                    File = fileDesc,
                    Folder = $"skillforge/{folder}",
                    Transformation = new Transformation().Quality("auto").FetchFormat("auto")
                };
                var result = _cloudinary.Upload(uploadParams);
                if (result.Error != null) throw new Exception($"Cloudinary Image Error: {result.Error.Message}");
                return result.SecureUrl.ToString();
            }
        }

        public string? SaveThumbnail(IFormFile? file)
        {
            if (file == null || file.Length == 0) return null;
            
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            string ext = Path.GetExtension(file.FileName).ToLower();
            if (!System.Linq.Enumerable.Contains(allowedExtensions, ext))
                throw new Exception("Only .jpg, .jpeg, .png, and .webp files are allowed");

            return UploadToCloudinary(file, "thumbnails", "image");
        }

        public string? HandleVideo(IFormFile? file, string? youtubeUrl, string? videoType)
        {
            // Priority 1: YouTube
            if (!string.IsNullOrWhiteSpace(youtubeUrl))
            {
                var trimmedUrl = youtubeUrl.Trim();
                if (!trimmedUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase) && 
                    (trimmedUrl.Contains("youtube.com") || trimmedUrl.Contains("youtu.be")))
                {
                    trimmedUrl = "https://" + trimmedUrl;
                }
                if (trimmedUrl.Contains("youtube.com") || trimmedUrl.Contains("youtu.be"))
                    return trimmedUrl;
            }

            // Priority 2: Cloudinary Video Upload
            if (file != null && file.Length > 0)
            {
                return UploadToCloudinary(file, "videos", "video");
            }

            return null;
        }

        public string? UploadResume(IFormFile? file)
        {
            if (file == null || file.Length == 0) return null;

            var allowedExtensions = new[] { ".pdf", ".doc", ".docx" };
            var ext = Path.GetExtension(file.FileName).ToLower();
            if (!System.Linq.Enumerable.Contains(allowedExtensions, ext))
                throw new Exception("Only PDF or Word documents are allowed.");

            return UploadToCloudinary(file, "resumes", "raw");
        }

        public string? SaveProfilePhoto(IFormFile? file)
        {
            if (file == null || file.Length == 0) return null;

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(file.FileName).ToLower();
            if (!System.Linq.Enumerable.Contains(allowedExtensions, ext))
                throw new Exception("Only images (.jpg, .jpeg, .png, .webp) are allowed.");

            return UploadToCloudinary(file, "profiles", "image");
        }
    }
}
