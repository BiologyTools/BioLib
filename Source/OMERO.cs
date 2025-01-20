using AForge;
using omero.api;
using omero.constants;
using omero.gateway.facility;
using omero.gateway.model;
using omero.gateway;
using omero.log;
using omero.model;
using omero;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gdk;
using ome.api;
using omero.romio;
using omero.sys;
using omero.model.enums;
using ome.formats.importer.cli;
using ome.formats.importer;
using ome.formats;
using loci.formats.@in;
using omero.grid;
using ome.util;
using ome.xml.model.enums;

namespace BioLib
{
    public class OMERO
    {
        public static string host, username;
        public static string password = "";
        public static int port;
        public static client client;
        public static ServiceFactoryPrx session;
        public static Gateway gateway;
        public static ExperimenterData experimenter;
        public static ExperimenterData experimenterSudo;
        public static IMetadataPrx meta;
        public static BrowseFacility browsefacil;
        public static MetadataFacility metafacil;
        public static DataManagerFacility datafacil;
        public static RawDataFacility rawdatafacil;
        public static EventContext adminContext;
        public static IAdminPrx adminPrx;
        public static SecurityContext sc;
        public static omero.api.RawPixelsStorePrx store;
        public static java.util.Collection datasets;
        public static java.util.Collection folders;
        public static java.util.Collection images;
        private static java.lang.Class brFacility = java.lang.Class.forName("omero.gateway.facility.BrowseFacility");
        private static java.lang.Class metaFacility = java.lang.Class.forName("omero.gateway.facility.MetadataFacility");
        private static java.lang.Class dataFacility = java.lang.Class.forName("omero.gateway.facility.DataManagerFacility");
        private static java.lang.Class rawFacility = java.lang.Class.forName("omero.gateway.facility.RawDataFacility");
        private static Random rng = new Random();
        public static double progress = 0;
        public static void Connect(string host, int port, string username, string password)
        {
            OMERO.host = host;
            OMERO.port = port;
            OMERO.username = username;
            OMERO.password = password;
            client = new client(host, port);
            session = client.createSession(username, password);
            // Initialize OMERO client and gateway
            gateway = new Gateway(new SimpleLogger());
            LoginCredentials credentials = new LoginCredentials();
            credentials.getServer().setHostname(host);
            credentials.getServer().setPort(port);
            credentials.getUser().setUsername(username);
            credentials.getUser().setPassword(password);
            experimenter = gateway.connect(credentials);
            Init();
        }
        public static void Connect()
        {
            client = new client(host, port);
            session = client.createSession(username, password);
            // Initialize OMERO client and gateway
            gateway = new Gateway(new SimpleLogger());
            LoginCredentials credentials = new LoginCredentials();
            credentials.getServer().setHostname(host);
            credentials.getServer().setPort(port);
            credentials.getUser().setUsername(username);
            credentials.getUser().setPassword(password);
            experimenter = gateway.connect(credentials);
            Init();
        }
        public static long GetID()
        {
            return rng.Next(0, 99999);
        }
        private static void Init()
        {
            meta = session.getMetadataService();
            ExperimenterGroupI sec = (ExperimenterGroupI)session.getSecurityContexts().get(0);
            RLong id = sec.getId();
            sc = new SecurityContext(id.getValue());
            browsefacil = (BrowseFacility)gateway.getFacility(brFacility);
            metafacil = (MetadataFacility)gateway.getFacility(metaFacility);
            datafacil = (DataManagerFacility)gateway.getFacility(dataFacility);
            rawdatafacil = (RawDataFacility)gateway.getFacility(rawFacility);
            folders = browsefacil.getFolders(sc);
            datasets = browsefacil.getDatasets(sc);
            images = browsefacil.getUserImages(sc);
            store = gateway.getPixelsStore(sc);
            adminPrx = session.getAdminService();

        }
        public static void ReConnect()
        {
            if (!gateway.isConnected())
            {
                client = new client(host, port);
                session = client.createSession(username, password);
                // Initialize OMERO client and gateway
                gateway = new Gateway(new SimpleLogger());
                LoginCredentials credentials = new LoginCredentials();
                credentials.getServer().setHostname(host);
                credentials.getServer().setPort(port);
                credentials.getUser().setUsername(username);
                credentials.getUser().setPassword(password);
                experimenter = gateway.connect(credentials);
            }
        }
        
        public static void Upload(BioImage b, long id)
        {
            //See above how to load an image.
            int sizeZ = b.SizeZ;
            int sizeT = b.SizeT;
            int sizeC = b.SizeC;
            int sizeX = b.SizeX;
            int sizeY = b.SizeY;

            //Read the pixels from the source image.
            RawPixelsStorePrx store = gateway.getPixelsStore(sc);
            List<byte[]> planes = new List<byte[]>();
            for (int z = 0; z < sizeZ; z++)
            {
                for (int c = 0; c < sizeC; c++)
                {
                    for (int t = 0; t < sizeT; t++)
                    {
                        planes.Add(b.GetBitmap(z,c,t).Bytes);
                    }
                }
            }

            //Now we are going to create the new image.
            IPixelsPrx proxy = gateway.getPixelsService(sc);

            //Create new image.
            String name = b.Filename;
            PixelsType pixelsType = new PixelsTypeI();
            pixelsType.setValue(omero.rtypes.rstring("uint8"));
            RLong idNew = proxy.createImage(sizeX, sizeY, sizeZ, sizeT, java.util.Arrays.asList(new java.lang.Integer(0)), pixelsType, name,"");
            IContainerPrx proxyCS = session.getContainerService();
            java.util.List results = proxyCS.getImages("omero.model.Image", java.util.Arrays.asList(new java.lang.Long(idNew.getValue())), new ParametersI());
            ImageData newImage = new ImageData((Image)results.get(0));

            //Link the new image and the dataset hosting the source image.
            DatasetImageLink link = new DatasetImageLinkI();
            link.setParent(new DatasetI(id, false));
            link.setChild(new ImageI(newImage.getId(), false));
            gateway.getUpdateService(sc).saveAndReturnObject(link);

            //Write the data.
            try
            {
                store = gateway.getPixelsStore(sc);
                store.setPixelsId(newImage.getDefaultPixels().getId(), false);
                int index = 0;
                for (int z = 0; z < sizeZ; z++)
                {
                    for (int c = 0; c < sizeC; c++)
                    {
                        for (int t = 0; t < sizeT; t++)
                        {
                            progress = ((float)index / (float)planes.Count);
                            store.setPlane(planes[index++], z, c, t);
                        }
                    }
                }
                //Save the data.
                store.save();
            }
            finally
            {
                store.close();
            }
        }
        public static BioImage GetImage(string filename, long dataset)
        {
            ReConnect();
            try
            {
                BioImage b = new BioImage("test.ome.tif");
                java.util.Collection col = new java.util.ArrayList();
                col.add(java.lang.Long.valueOf(dataset));
                var uims = browsefacil.getImagesForDatasets(sc, col);
                var itr = uims.iterator();
                java.util.List li = new java.util.ArrayList();
                java.util.ArrayList imgs = new java.util.ArrayList();
                do
                {
                    java.util.ArrayList list = new java.util.ArrayList();
                    ImageData o = (ImageData)itr.next();
                    string name = o.getName();
                    if (name != filename)
                        continue;
                    PixelsData pd = o.getDefaultPixels();
                    int xs = pd.getSizeX();
                    int ys = pd.getSizeY();
                    int zs = pd.getSizeZ();
                    int cs = pd.getSizeC();
                    int ts = pd.getSizeT();
                    long pid = o.getId();

                    b.Filename = name;
                    b.ID = name;
                    RawPixelsStorePrx store = gateway.getPixelsStore(sc);
                    long ind = o.getId();
                    long ll = pd.getId();
                    list.add(java.lang.Long.valueOf(ind));
                    try
                    {
                        var acq = metafacil.getChannelData(sc, ind);
                        int s = acq.size();
                        for (int i = 0; i < s; i++)
                        {
                            var ch = (ChannelData)acq.get(i);
                            var ac = metafacil.getImageAcquisitionData(sc, ind);
                            var chan = ch.asChannel();
                            AForge.Color color = AForge.Color.FromArgb(chan.getRed().getValue(), chan.getGreen().getValue(), chan.getBlue().getValue());
                            var px = pd.asPixels().getPixelsType();
                            int bits = px.getBitSize().getValue();
                            AForge.PixelFormat pxx = GetPixelFormat(bits);
                            AForge.Channel cch = null;
                            if (pxx == AForge.PixelFormat.Format8bppIndexed || pxx == AForge.PixelFormat.Format16bppGrayScale)
                                cch = new AForge.Channel(i, bits, 1);
                            else if (pxx == AForge.PixelFormat.Format24bppRgb || pxx == AForge.PixelFormat.Format48bppRgb)
                                cch = new AForge.Channel(i, bits, 3);
                            else if (pxx == AForge.PixelFormat.Format32bppArgb)
                                cch = new AForge.Channel(i, bits, 4);
                            cch.Fluor = ch.getFluor();
                            var em = ch.getEmissionWavelength(omero.model.enums.UnitsLength.NANOMETER);
                            if (em != null)
                                cch.Emission = (int)em.getValue();
                            cch.Color = color;
                            cch.Name = ch.getName();
                            if (cch.Name == null)
                                cch.Name = i.ToString();
                            b.Channels.Add(cch);
                        }
                    }
                    catch (Exception exx)
                    {
                        Console.WriteLine(exx.Message);
                    }
                    var stage = o.asImage().getStageLabel();
                    
                    try
                    {
                        int i = 0;
                        while(true)
                        {
                            omero.RInt rint = rtypes.rint(i);
                            Image im = o.asImage();
                            im.setSeries(rint);
                            RInt rin = im.getSeries();
                            AForge.PixelFormat px = AForge.PixelFormat.Format8bppIndexed;
                            Pixels pxs = im.getPixels(0);
                            store.setPixelsId(pxs.getId().getValue(),true);
                            if(!store.requiresPixelsPyramid())
                            {
                                var pxto = pxs.getPixelsType();
                                int bitso = pxto.getBitSize().getValue();
                                int wo = pxs.getSizeX().getValue();
                                int ho = pxs.getSizeY().getValue();
                                px = GetPixelFormat(bitso);
                                double pxxo = pxs.getPhysicalSizeX().getValue();
                                double pyyo = pxs.getPhysicalSizeY().getValue();
                                double pzzo = 0;
                                var pzo = pxs.getPhysicalSizeZ();
                                if (pzo != null)
                                    pzzo = pzo.getValue();
                                if (stage != null)
                                {
                                    var sta = (StageLabel)session.getQueryService().get("StageLabel", o.asImage().getStageLabel().getId().getValue());
                                    Length? sxxo = sta.getPositionX();
                                    Length? syyo = sta.getPositionY();
                                    Length? szzo = sta.getPositionZ();
                                    b.Resolutions.Add(new Resolution(wo, ho, px, pxxo * wo, pyyo * ho, pzzo, sxxo.getValue(), syyo.getValue(), szzo.getValue()));
                                }
                                else
                                    b.Resolutions.Add(new Resolution(wo, ho, px, pxxo * wo, pyyo * ho, pzzo, 0, 0, 0));
                                break;
                            }
                            else
                            {
                                b.Type = BioImage.ImageType.pyramidal;
                            }
                            if (i == store.getResolutionLevels())
                                break;
                            var ress = store.getResolutionDescriptions();
                            var re = ress[i];
                            var pxt = pxs.getPixelsType();
                            int bits = pxt.getBitSize().getValue();
                            int w = re.sizeX;
                            int h = re.sizeY;
                            px = GetPixelFormat(bits);
                            double pxx = pxs.getPhysicalSizeX().getValue();
                            double pyy = pxs.getPhysicalSizeY().getValue();
                            double pzz = 0;
                            var pz = pxs.getPhysicalSizeZ();
                            if(pz != null)
                                pzz = pz.getValue();
                            if (stage != null)
                            {
                                var sta = (StageLabel)session.getQueryService().get("StageLabel", o.asImage().getStageLabel().getId().getValue());
                                Length? sxx = sta.getPositionX();
                                Length? syy = sta.getPositionY();
                                Length? szz = sta.getPositionZ();
                                b.Resolutions.Add(new Resolution(w, h, px, pxx, pyy, pzz, sxx.getValue(), syy.getValue(), szz.getValue()));
                            }
                            else
                                b.Resolutions.Add(new Resolution(w, h, px, pxx, pyy, pzz, 0, 0, 0));
                            i++;
                        }
                    }
                    catch (Exception ex)
                    {
                        AForge.PixelFormat px = AForge.PixelFormat.Format8bppIndexed;
                        var pxt = pd.asPixels().getPixelsType();
                        int bits = pxt.getBitSize().getValue();
                        px = GetPixelFormat(bits);
                        double pxx = pd.asPixels().getPhysicalSizeX().getValue();
                        double pyy = pd.asPixels().getPhysicalSizeY().getValue();
                        double pzz = 0;
                        var pz = pd.asPixels().getPhysicalSizeZ();
                        if (pz != null)
                            pzz = pz.getValue();
                        if (stage != null)
                        {
                            if (stage.isLoaded())
                            {
                                Length? sxx = stage.getPositionX();
                                Length? syy = stage.getPositionY();
                                Length? szz = stage.getPositionZ();
                                b.Resolutions.Add(new Resolution(xs, ys, px, pxx, pyy, pzz, sxx.getValue(), syy.getValue(), szz.getValue()));
                            }
                        }
                        else
                            b.Resolutions.Add(new Resolution(xs, ys, px, pxx, pyy, pzz, 0, 0, 0));
                    }

                    for (int z = 0; z < zs; z++)
                    {
                        for (int c = 0; c < cs; c++)
                        {
                            for (int t = 0; t < ts; t++)
                            {
                                if (b.isPyramidal)
                                {
                                    Pixels ps = pd.asPixels();
                                    int chc = ps.sizeOfChannels();
                                    store.setPixelsId(ps.getId().getValue(), true);
                                    store.setResolutionLevel(0);
                                    if (xs > 1920 || ys > 1080)
                                    {
                                        byte[] bts = store.getTile(z, c, t, 0, 0, 1920, 1080);
                                        PixelsType pxt = ps.getPixelsType();
                                        AForge.PixelFormat px = AForge.PixelFormat.Format8bppIndexed;
                                        int bits = pxt.getBitSize().getValue();
                                        px = GetPixelFormat(bits);
                                        b.Buffers.Add(new AForge.Bitmap("", 1920, 1080, px, bts, new AForge.ZCT(z, c, t), 0, null, false));
                                    }
                                    else
                                    {
                                        byte[] bts = store.getTile(z, c, t, 0, 0, xs, ys);
                                        PixelsType pxt = ps.getPixelsType();
                                        AForge.PixelFormat px = AForge.PixelFormat.Format8bppIndexed;
                                        int bits = pxt.getBitSize().getValue();
                                        px = GetPixelFormat(bits);
                                        b.Buffers.Add(new AForge.Bitmap("", xs, ys, px, bts, new AForge.ZCT(z, c, t), 0, null, false));
                                    }
                                }
                                else
                                {
                                    Pixels ps = pd.asPixels();
                                    int chc = ps.sizeOfChannels();
                                    store.setPixelsId(ps.getId().getValue(), true);
                                    byte[] bts = store.getPlane(z, c, t);
                                    PixelsType pxt = ps.getPixelsType();
                                    AForge.PixelFormat px = AForge.PixelFormat.Format8bppIndexed;
                                    int bits = pxt.getBitSize().getValue();
                                    px = GetPixelFormat(bits);
                                    b.Buffers.Add(new AForge.Bitmap("", xs, ys, px, bts, new AForge.ZCT(z, c, t), 0, null, false));
                                }
                            }
                        }
                    }
                    b.UpdateCoords(zs, cs, ts);
                    b.Volume = new VolumeD(new Point3D(b.StageSizeX, b.StageSizeY, b.StageSizeZ), new Point3D(b.SizeX * b.PhysicalSizeX, b.SizeY * b.PhysicalSizeY, b.SizeZ * b.PhysicalSizeZ));
                    b.bitsPerPixel = b.Buffers[0].BitsPerPixel;
                    b.series = o.getSeries();
                    b.imagesPerSeries = b.Buffers.Count;
                    b.rgbChannels = new int[b.Channels.Count];
                    b.rgbChannels[0] = 0;
                    b.rgbChannels[1] = 1;
                    b.rgbChannels[2] = 2;
                    BioImage.AutoThreshold(b, true);
                    if (b.bitsPerPixel > 8)
                        b.StackThreshold(true);
                    else
                        b.StackThreshold(false);
                    b.Tag = "OMERO";
                    if(b.isPyramidal)
                    {
                        b.SlideBase = new SlideBase(b, SlideImage.Open(b));
                    }
                    return b;
                }
                while (itr.hasNext());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            return null;
        }
        public static BioImage GetImage(long id)
        {
            string n = browsefacil.getImage(sc, id).getName();
            return GetImage(n,id);
        }
        public static Bitmap GetTile(BioImage b, ZCT coord, int x, int y, int width, int height, int level)
        {
            ReConnect();
            var itr = images.iterator();
            java.util.List li = new java.util.ArrayList();
            java.util.ArrayList imgs = new java.util.ArrayList();
            do
            {
                java.util.ArrayList list = new java.util.ArrayList();
                ImageData o = (ImageData)itr.next();
                string name = o.getName();
                if (name != b.Filename)
                    continue;
                PixelsData pd = o.getDefaultPixels();
                int zs = pd.getSizeZ();
                int cs = pd.getSizeC();
                int ts = pd.getSizeT();
                long pid = o.getId();
                b.Filename = name;
                b.ID = name;
                long ind = o.getId();
                long ll = pd.getId();
                list.add(java.lang.Long.valueOf(ind));
                Pixels ps = pd.asPixels();
                int chc = ps.sizeOfChannels();
                store.setPixelsId(ps.getId().getValue(), true);
                store.setResolutionLevel(level);
                byte[] bts = store.getTile(coord.Z, coord.C, coord.T, x, y, width, height);
                PixelsType pxt = ps.getPixelsType();
                AForge.PixelFormat px = AForge.PixelFormat.Format8bppIndexed;
                int bits = pxt.getBitSize().getValue();
                px = GetPixelFormat(bits);
                return new AForge.Bitmap("", width, height, px, bts, coord, 0, null, false);
            } while (itr.hasNext());
            return null;
        }
        public static Dictionary<long,Pixbuf> GetThumbnails(string[] filenames, int width, int height)
        {
            ReConnect();
            Dictionary<long, Pixbuf> dict = new Dictionary<long, Pixbuf>();
            try
            {
                var meta = session.getMetadataService();
                ExperimenterGroupI sec = (ExperimenterGroupI)session.getSecurityContexts().get(0);
                RLong id = sec.getId();
                SecurityContext sc = new SecurityContext(id.getValue());
                // Get the thumbnail service
                ThumbnailStorePrx store = gateway.getThumbnailService(sc);
                // Access BrowseFacility
                BrowseFacility facility = (BrowseFacility)gateway.getFacility(brFacility);
                var uims = facility.getUserImages(sc);
                var itr = uims.iterator();
                List<Pixbuf> images = new List<Pixbuf>();   
                while (itr.hasNext())
                {
                    var img = (ImageData)itr.next();
                    string name = img.getName();
                    for (int i = 0; i < filenames.Length; i++)
                    {
                        if(name == filenames[i])
                        {
                            // Set the pixels ID for the image
                            long pixelId = img.getDefaultPixels().getId();
                            store.setPixelsId(pixelId);
                            byte[] thumbnailBytes = store.getThumbnail(omero.rtypes.rint(width), omero.rtypes.rint(height));
                            Pixbuf pf = new Pixbuf(thumbnailBytes);
                            dict.Add(img.getId(), pf);
                        }
                    }
                }
                return dict;
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }
        public static string GetNameFromID(long id)
        {
            ReConnect();
            return browsefacil.getImage(sc, id).getName();
        }
        private static PixelFormat GetPixelFormat(int bits)
        {
            PixelFormat px = PixelFormat.Format8bppIndexed;
            if (bits == 8)
                px = AForge.PixelFormat.Format8bppIndexed;
            else if (bits < 16 || bits == 16)
                px = AForge.PixelFormat.Format16bppGrayScale;
            else if (bits > 16 && bits <= 24)
                px = AForge.PixelFormat.Format24bppRgb;
            else if (bits == 32)
                px = PixelFormat.Format32bppArgb;
            else if (bits > 32)
                px = AForge.PixelFormat.Format48bppRgb;
            return px;
        }
        public static List<string> GetAllFiles()
        {
            ReConnect();
            var meta = session.getMetadataService();
            var uims = browsefacil.getUserImages(sc);
            List<string> files = new List<string>();
            var itr = uims.iterator();
            while (itr.hasNext())
            {
                ImageData da = (ImageData)itr.next();
                files.Add(da.getName());
            }
            return files;
        }
        public static List<string> GetDatasets()
        {
            ReConnect();
            var d = browsefacil.getDatasets(sc);
            var itr = d.iterator();
            List<string> dbs = new List<string>();
            while (itr.hasNext())
            {
                DatasetData dd = (DatasetData)itr.next();
                dbs.Add(dd.getName());
            }
            return dbs;
        }
        public static List<DatasetData> GetDatasetsData()
        {
            ReConnect();
            var d = browsefacil.getDatasets(sc);
            var itr = d.iterator();
            List<DatasetData> dbs = new List<DatasetData>();
            while (itr.hasNext())
            {
                DatasetData dd = (DatasetData)itr.next();
                dbs.Add(dd);
            }
            return dbs;
        }
        public static DatasetData GetDataset(string name)
        {
            ReConnect();
            var d = browsefacil.getDatasets(sc);
            var itr = d.iterator();
            while (itr.hasNext())
            {
                DatasetData dd = (DatasetData)itr.next();
                if (dd.getName() == name)
                    return dd;
            }
            return null;
        }
        public static DatasetData GetDataset(string name, long id)
        {
            ReConnect();
            var d = browsefacil.getDatasets(sc);
            var itr = d.iterator();
            while (itr.hasNext())
            {
                DatasetData dd = (DatasetData)itr.next();
                if (dd.getName() == name && id == dd.getId())
                    return dd;
            }
            return null;
        }
        public static List<string> GetFolders()
        {
            ReConnect();
            var d = browsefacil.getFolders(sc);
            var itr = d.iterator();
            List<string> dbs = new List<string>();
            while (itr.hasNext())
            {
                dbs.Add((string)itr.next());
            }
            return dbs;
        }
        public static List<string> GetDatasetFiles(string db)
        {
            ReConnect();
            var d = browsefacil.getDatasets(sc);
            var itr = d.iterator();
            while (itr.hasNext())
            {
                DatasetData idr = (DatasetData)itr.next();
                if(idr.getName() == db)
                {
                    java.util.ArrayList ar = new java.util.ArrayList();
                    ar.add(java.lang.Long.valueOf(idr.getId()));
                    var ims = browsefacil.getImagesForDatasets(sc,ar);
                    var itr2 = ims.iterator();
                    List<string> fs = new List<string>();
                    while (itr2.hasNext())
                    {
                        var im = (ImageData)itr2.next();
                        fs.Add(im.getName());
                    }
                    return fs;
                }
            }
            return null;
        }
        public static List<string> GetDatasetFiles(long dbid)
        {
            ReConnect();
            var d = browsefacil.getDatasets(sc);
            var itr = d.iterator();
            while (itr.hasNext())
            {
                DatasetData idr = (DatasetData)itr.next();
                if (dbid == idr.getId())
                {
                    java.util.ArrayList ar = new java.util.ArrayList();
                    ar.add(java.lang.Long.valueOf(idr.getId()));
                    var imgs = browsefacil.getImagesForDatasets(sc,ar);
                    var itr2 = imgs.iterator();
                    List<string> str = new List<string>();
                    while (itr2.hasNext())
                    {
                        ImageData imageData = (ImageData)itr2.next();
                        str.Add(imageData.getName());
                    }
                    return str;
                }
            }
            return null;
        }
        public static List<long> GetDatasetIds(long dbid)
        {
            ReConnect();
            var d = browsefacil.getDatasets(sc);
            var itr = d.iterator();
            while (itr.hasNext())
            {
                DatasetData idr = (DatasetData)itr.next();
                if (dbid == idr.getId())
                {
                    java.util.ArrayList ar = new java.util.ArrayList();
                    ar.add(java.lang.Long.valueOf(idr.getId()));
                    var imgs = browsefacil.getImagesForDatasets(sc, ar);
                    var itr2 = imgs.iterator();
                    List<long> str = new List<long>();
                    while (itr2.hasNext())
                    {
                        ImageData imageData = (ImageData)itr2.next();
                        str.Add(imageData.getId());
                    }
                    return str;
                }
            }
            return null;
        }
    }
}
