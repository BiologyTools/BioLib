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

namespace BioLib
{
    public class OMERO
    {
        public static string host, username;
        private static string password;
        public static int port;
        public static client client;
        public static ServiceFactoryPrx session;
        public static Gateway gateway;
        public static ExperimenterData experimenter;
        private static java.lang.Class brFacility = java.lang.Class.forName("omero.gateway.facility.BrowseFacility");
        private static java.lang.Class metaFacility = java.lang.Class.forName("omero.gateway.facility.MetadataFacility");
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
        }
        public static void ReConnect()
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
        public static BioImage GetImage(string filename)
        {
            try
            {
                BioImage b = new BioImage("test.ome.tif");
                var meta = session.getMetadataService();
                ExperimenterGroupI sec = (ExperimenterGroupI)session.getSecurityContexts().get(0);
                RLong id = sec.getId();
                SecurityContext sc = new SecurityContext(id.getValue());
                // Access BrowseFacility
                BrowseFacility facility = (BrowseFacility)gateway.getFacility(brFacility);
                MetadataFacility mf = (MetadataFacility)gateway.getFacility(metaFacility);
                var fols = facility.getFolders(sc);
                var d = facility.getDatasets(sc);
                var uims = facility.getUserImages(sc);
                var itr = uims.iterator();
                java.util.List li = new java.util.ArrayList();
                java.util.ArrayList imgs = new java.util.ArrayList();
                do
                {
                    java.util.ArrayList list = new java.util.ArrayList();
                    omero.gateway.model.ImageData o = (omero.gateway.model.ImageData)itr.next();
                    string name = o.getName();
                    if (name != filename)
                        continue;
                    b.Filename = name;
                    b.ID = name;
                    RawPixelsStorePrx store = gateway.getPixelsStore(sc);
                    PixelsData pd = o.getDefaultPixels();
                    int xs = pd.getSizeX();
                    int ys = pd.getSizeY();
                    int zs = pd.getSizeZ();
                    int cs = pd.getSizeC();
                    int ts = pd.getSizeT();
                    long ind = o.getId();
                    long ll = pd.getId();
                    list.add(java.lang.Long.valueOf(ind));
                    try
                    {
                        var acq = mf.getChannelData(sc, ind);
                        int s = acq.size();
                        for (int i = 0; i < s - 1; i++)
                        {
                            var ch = (ChannelData)acq.get(i);
                            var ac = mf.getImageAcquisitionData(sc, ind);
                            var chan = ch.asChannel();
                            AForge.Color col = AForge.Color.FromArgb(chan.getRed().getValue(), chan.getGreen().getValue(), chan.getBlue().getValue());
                            var px = pd.asPixels().getPixelsType();
                            int bits = px.getBitSize().getValue();
                            AForge.PixelFormat pxx = AForge.PixelFormat.Format8bppIndexed;
                            if (bits == 8)
                                pxx = AForge.PixelFormat.Format8bppIndexed;
                            else if (bits < 16 || bits == 16)
                                pxx = AForge.PixelFormat.Format16bppGrayScale;
                            else if (bits == 24)
                                pxx = AForge.PixelFormat.Format24bppRgb;
                            else if (bits == 32)
                                pxx = AForge.PixelFormat.Format32bppArgb;
                            else if (bits > 32)
                                pxx = AForge.PixelFormat.Format48bppRgb;
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
                            cch.Color = col;
                            cch.Name = ch.getName();
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
                        for (int l = 0; l < store.getResolutionLevels(); l++)
                        {
                            store.setResolutionLevel(l);
                            ResolutionDescription[] res = store.getResolutionDescriptions();
                            AForge.PixelFormat px = AForge.PixelFormat.Format8bppIndexed;
                            Pixels pxs = pd.asPixels();
                            var pxt = pxs.getPixelsType();
                            int bits = pxt.getBitSize().getValue();
                            if (bits == 8)
                                px = AForge.PixelFormat.Format8bppIndexed;
                            else if (bits < 16 || bits == 16)
                                px = AForge.PixelFormat.Format16bppGrayScale;
                            else if (bits > 16 && bits <= 32)
                                px = AForge.PixelFormat.Format32bppArgb;
                            else if (bits > 32)
                                px = AForge.PixelFormat.Format48bppRgb;
                            double pxx = pxs.getPhysicalSizeX().getValue();
                            double pyy = pxs.getPhysicalSizeX().getValue();
                            double pzz = pxs.getPhysicalSizeX().getValue();
                            if (stage.isLoaded())
                            {
                                Length? sxx = stage.getPositionX();
                                Length? syy = stage.getPositionY();
                                Length? szz = stage.getPositionZ();
                                b.Resolutions.Add(new Resolution(xs, ys, px, pxx, pyy, pzz, sxx.getValue(), syy.getValue(), szz.getValue()));
                            }
                            else
                                b.Resolutions.Add(new Resolution(xs, ys, px, pxx, pyy, pzz, 0, 0, 0));
                        }
                    }
                    catch (Exception ex)
                    {
                        AForge.PixelFormat px = AForge.PixelFormat.Format8bppIndexed;
                        var pxt = pd.asPixels().getPixelsType();
                        int bits = pxt.getBitSize().getValue();
                        if (bits == 8)
                            px = AForge.PixelFormat.Format8bppIndexed;
                        else if (bits < 16 || bits == 16)
                            px = AForge.PixelFormat.Format16bppGrayScale;
                        else if (bits > 16 && bits <= 32)
                            px = AForge.PixelFormat.Format32bppArgb;
                        else if (bits > 32)
                            px = AForge.PixelFormat.Format48bppRgb;
                        double pxx = pd.asPixels().getPhysicalSizeX().getValue();
                        double pyy = pd.asPixels().getPhysicalSizeX().getValue();
                        double pzz = pd.asPixels().getPhysicalSizeX().getValue();
                        if (stage.isLoaded())
                        {
                            Length? sxx = stage.getPositionX();
                            Length? syy = stage.getPositionY();
                            Length? szz = stage.getPositionZ();
                            b.Resolutions.Add(new Resolution(xs, ys, px, pxx, pyy, pzz, sxx.getValue(), syy.getValue(), szz.getValue()));
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
                                Pixels ps = pd.asPixels();
                                int chc = ps.sizeOfChannels();
                                store.setPixelsId(ps.getId().getValue(), true);
                                byte[] bts = store.getPlane(z, c, t);
                                PixelsType pxt = ps.getPixelsType();
                                AForge.PixelFormat px = AForge.PixelFormat.Format8bppIndexed;
                                int bits = pxt.getBitSize().getValue();
                                if (bits == 8)
                                    px = AForge.PixelFormat.Format8bppIndexed;
                                else if (bits < 16 || bits == 16)
                                    px = AForge.PixelFormat.Format16bppGrayScale;
                                else if (bits > 16 && bits <= 32)
                                    px = AForge.PixelFormat.Format32bppArgb;
                                else if (bits > 32)
                                    px = AForge.PixelFormat.Format48bppRgb;
                                b.Buffers.Add(new AForge.Bitmap(xs, ys, px, bts, new AForge.ZCT(z, c, t), ""));
                            }
                        }
                    }
                    b.UpdateCoords(zs, cs, ts);
                    b.Volume = new VolumeD(new Point3D(b.StageSizeX, b.StageSizeY, b.StageSizeZ), new Point3D(b.SizeX * b.PhysicalSizeX, b.SizeY * b.PhysicalSizeY, b.SizeZ * b.PhysicalSizeZ));
                    b.bitsPerPixel = b.Buffers[0].BitsPerPixel;
                    b.series = o.getSeries();
                    BioImage.AutoThreshold(b, true);
                    if (b.bitsPerPixel > 8)
                        b.StackThreshold(true);
                    else
                        b.StackThreshold(false);
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
        public static Bitmap[] GetThumbnails(string[] filenames, int width, int height)
        {
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
                List<Bitmap> images = new List<Bitmap>();   
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
                            Bitmap bm = new Bitmap(width, height, PixelFormat.Format32bppArgb, thumbnailBytes, new ZCT(), "");
                            images.Add(bm);
                        }
                    }
                }
                return images.ToArray();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
        }
        public static List<string> GetAllFiles()
        {
            var meta = session.getMetadataService();
            ExperimenterGroupI sec = (ExperimenterGroupI)session.getSecurityContexts().get(0);
            RLong id = sec.getId();
            SecurityContext sc = new SecurityContext(id.getValue());
            // Access BrowseFacility
            BrowseFacility facility = (BrowseFacility)gateway.getFacility(brFacility);
            MetadataFacility mf = (MetadataFacility)gateway.getFacility(metaFacility);
            var fols = facility.getFolders(sc);
            var d = facility.getDatasets(sc);
            var uims = facility.getUserImages(sc);
            List<string> files = new List<string>();
            var itr = uims.iterator();
            while (itr.hasNext())
            {
                ImageData da = (ImageData)itr.next();
                files.Add(da.getName());
            }
            return files;
        }
        public static List<string> GetDatabases()
        {
            var meta = session.getMetadataService();
            ExperimenterGroupI sec = (ExperimenterGroupI)session.getSecurityContexts().get(0);
            RLong id = sec.getId();
            SecurityContext sc = new SecurityContext(id.getValue());
            // Access BrowseFacility
            BrowseFacility facility = (BrowseFacility)gateway.getFacility(brFacility);
            MetadataFacility mf = (MetadataFacility)gateway.getFacility(metaFacility);
            var d = facility.getDatasets(sc);
            var itr = d.iterator();
            List<string> dbs = new List<string>();
            while (itr.hasNext())
            {
                DatasetData dd = (DatasetData)itr.next();
                dbs.Add(dd.getName());
            }
            return dbs;
        }
        public static List<string> GetFolders()
        {
            var meta = session.getMetadataService();
            ExperimenterGroupI sec = (ExperimenterGroupI)session.getSecurityContexts().get(0);
            RLong id = sec.getId();
            SecurityContext sc = new SecurityContext(id.getValue());
            // Access BrowseFacility
            BrowseFacility facility = (BrowseFacility)gateway.getFacility(brFacility);
            MetadataFacility mf = (MetadataFacility)gateway.getFacility(metaFacility);
            var d = facility.getFolders(sc);
            var itr = d.iterator();
            List<string> dbs = new List<string>();
            while (itr.hasNext())
            {
                dbs.Add((string)itr.next());
            }
            return dbs;
        }
        public static List<string> GetDatabaseFiles(string db)
        {
            var meta = session.getMetadataService();
            ExperimenterGroupI sec = (ExperimenterGroupI)session.getSecurityContexts().get(0);
            RLong id = sec.getId();
            SecurityContext sc = new SecurityContext(id.getValue());
            // Access BrowseFacility
            BrowseFacility facility = (BrowseFacility)gateway.getFacility(brFacility);
            MetadataFacility mf = (MetadataFacility)gateway.getFacility(metaFacility);
            var d = facility.getDatasets(sc);
            var itr = d.iterator();
            while (itr.hasNext())
            {
                DatasetData idr = (DatasetData)itr.next();
                if(idr.getName() == db)
                {
                    java.util.ArrayList ar = new java.util.ArrayList();
                    ar.add(java.lang.Long.valueOf(idr.getId()));
                    var ims = facility.getImagesForDatasets(sc,ar);
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
        public static List<string> GetDatabaseFiles(long dbid)
        {
            var meta = session.getMetadataService();
            ExperimenterGroupI sec = (ExperimenterGroupI)session.getSecurityContexts().get(0);
            RLong id = sec.getId();
            SecurityContext sc = new SecurityContext(id.getValue());
            // Access BrowseFacility
            BrowseFacility facility = (BrowseFacility)gateway.getFacility(brFacility);
            MetadataFacility mf = (MetadataFacility)gateway.getFacility(metaFacility);
            var d = facility.getDatasets(sc);
            var itr = d.iterator();
            while (itr.hasNext())
            {
                Dataset idr = (Dataset)itr.next();
                if (dbid == idr.getId().getValue())
                {
                    java.util.ArrayList ar = new java.util.ArrayList();
                    ar.add(idr);
                    var imgs = facility.getImagesForDatasets(sc,ar);
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
    }
}
