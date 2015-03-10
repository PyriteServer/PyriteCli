using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CuberLib
{
    public class ImageTile
    {
        private Image image;
        private Size size;

        public ImageTile(string inputFile, int xSize, int ySize)
        {
            if (!File.Exists(inputFile)) throw new FileNotFoundException();

            image = Image.FromFile(inputFile);
            size = new Size(xSize, ySize);
        }

        public void GenerateTiles(string outputPath)
        {
            int xMax = image.Width;
            int yMax = image.Height;
            int tileWidth = xMax / size.Width;
            int tileHeight = yMax / size.Height;

            for (int x = 0; x < size.Width; x++)
            {
                for (int y = 0; y < size.Height; y++)
                {
                    string outputFileName = Path.Combine(outputPath, string.Format("{0}_{1}.jpg", x, y));

                    Rectangle tileBounds = new Rectangle(x * tileWidth, y * tileHeight, tileWidth, tileHeight);
                    Bitmap target = new Bitmap(tileWidth, tileHeight);

                    using (Graphics graphics = Graphics.FromImage(target))
                    {
                        graphics.DrawImage(
                            image, 
                            new Rectangle(0, 0, tileWidth, tileHeight),
                            tileBounds,
                            GraphicsUnit.Pixel);
                    }

                    target.Save(outputFileName, ImageFormat.Jpeg);
                }
            }
        }
    }
}
