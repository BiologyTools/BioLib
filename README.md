# BioLib
![Nuget](https://img.shields.io/nuget/dt/BioLib) [![DOI](https://zenodo.org/badge/DOI/10.5281/zenodo.8127022.svg)](https://doi.org/10.5281/zenodo.8127022)

A GUI-less version of Bio .NET library for editing & annotating various microscopy image formats. Supports all bioformats supported images. Integrates with ImageJ, running ImageJ filters & macro functions. Supports Windows, Linux and Mac. Check out the [documentation.](https://biologytools.github.io/Documentation/BioLib/index.html)

## Usage
```
//First call BioImage.Initialize to 
//initialize the Bioformats library.
BioImage.Initialize();

//Once initialized you can open OME, ImageJ tiff files, and Bio Tiff files with:
BioImage b = BioImage.OpenFile("file");

//Or if you want to use specifically the OME image reader you can use BioImage.OpenOME
BioImage b = BioImage.OpenOME("file");

//If you are working with a pyramidal image you can open a portion of a tiled image with OpenOME.
//BioImage.OpenOME(string file, int serie, bool tab, bool addToImages, bool tile, int tilex, int tiley, int tileSizeX, int tileSizeY)

//You can specify whether to open in a newtab as well as whether to add the image to 
//the Images.images table. As well as specify whether to open as a tile with the specified 
//tile X,Y position & tile width & height.    
BioImage.OpenOME("file",0,false,false,true,0,0,600,600);
//This will open a portion of the image as a tile and won't add it to the Images table.

//Once you have opened a tiled image with BioImage.OpenOME you can call the 
//GetTile(BioImage b, ZCT coord, int serie, int tilex, int tiley, int tileSizeX, int tileSizeY) method
// to quickly get another tile from different portion of the image. For BioGTK & BioLib
Bitmap bm = BioImage.GetTile(b, new ZCT(0,0,0), 0, 100, 100, 600, 600);

//To get the current coordinate of the ImageView you can call GetCoordinate.
ZCT cord = v.GetCoordinate();
//or to set the current coordinate
v.SetCoordinate(new ZCT(1,1,1));

//To create a point as well as any other ROI type you can call the ROI create methods.
ROI p = ROI.CreatePoint(cord, 0, 0);
ROI rect = ROI.CreateRectangle(cord, 0, 0, 100, 100);

//Usage of Graphics class for 16 & 48 bit images as well as regular bit depth images
//is very similar to System.Graphics.
//We create a new Graphics object by passing the Bitmap for BioGTK & BioLib and BufferInfo for BioCore
Graphics g = Graphics.FromImage(b.Buffers[0]);

//Then we create a pen by passing a ColorS which represent a Color with, 
//a higher bit depth (unsigned short) rather than a byte.
g.pen = new Pen(new ColorS(ushort.MaxValue, ushort.MaxValue, ushort.MaxValue));

//Then we can call the familiar methods DrawLine, DrawPolygon, FillPolygon etc.
g.DrawLine(0,0,100,100);
//Finally we dispose the Graphics object.
g.Dispose();

//We can also save the resulting image given the ID of the image in the Images table.
//All images opened with BioImage.OpenFile or BioImage.OpenOME are added to the 
//Images.images table with the filename as an ID.
BioImage.SaveFile("file","path");

BioImage[] bms = new BioImage[]{BioImage.OpenFile("test.ome.tif")};
QuPath.Project FromImages(bms, string quPathProjectFile);
QuPath.Project.SaveProject(string file, bms);

//To convert between different pixel formats we can call for example To24Bit.
b.To24Bit();
```
