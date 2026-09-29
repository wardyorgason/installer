using Installer.Dao.Wrappers;

namespace Installer.Dao.Imaging;

internal sealed class ImageDao(IFileSystemWrapper fileSystem, IImageWrapper images) : IImageDao
{
    public (int Width, int Height) GetSize(string pngPath) => images.GetSize(fileSystem.ReadAllBytes(pngPath));

    public byte[] RenderPng(string pngPath, int size) => images.ResizeSquare(fileSystem.ReadAllBytes(pngPath), size);
}
