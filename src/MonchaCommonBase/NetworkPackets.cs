
#region Usings
using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization.Formatters.Binary;
#endregion

namespace MonchaCommonBase {

    #region Abstraction
    [Serializable]
    public abstract class NetworkPacket {

        #region Lifecycle
        public NetworkPacket() { }
        #endregion

    }
    #endregion

    #region Management
    [Serializable]
    public class ManagementPacket : NetworkPacket {

        #region Fields
        private bool openDll;
        #endregion

        #region Lifecycle
        public ManagementPacket() : base() { }
        #endregion

        #region Properties
        public bool OpenDll {
            get { return openDll; }
        }
        #endregion

        #region Functions
        public void SetOpenDll() {
            openDll=true;
        }

        public void SetCloseDll() {
            openDll=false;
        }
        #endregion

    }
    #endregion

    #region Messaging
    [Serializable]
    public class MessagePacket : NetworkPacket {

        #region Fields
        private string message;
        #endregion

        #region Lifecycle
        public MessagePacket() : base() { }
        #endregion

        #region Properties
        public string Message {
            set { message=value; }
            get { return message; }
        }
        #endregion

    }

    [Serializable]
    public class ExceptionCodePacket : NetworkPacket {

        #region Fields
        private string deviceAddress;
        private int code;
        private string message;
        #endregion

        #region Lifecycle
        public ExceptionCodePacket() : base() { }
        #endregion

        #region Properties
        public string DeviceAddress {
            set { deviceAddress=value; }
            get { return deviceAddress; }
        }

        public int Code {
            set { code=value; }
            get { return code; }
        }

        public string Message {
            set { message=value; }
            get { return message; }
        }
        #endregion

    }
    #endregion

    #region Search Devices
    [Serializable]
    public class SearchDevicesPacket : NetworkPacket {

        #region Lifecycle
        public SearchDevicesPacket() : base() { }
        #endregion

    }

    [Serializable]
    public class SearchDevicesResultPacket : NetworkPacket {

        #region Fields
        private List<LaserController> laserControllers;
        #endregion

        #region Lifecycle
        public SearchDevicesResultPacket() : base() {
            laserControllers=new List<LaserController>();
        }
        #endregion

        #region Properties
        public List<LaserController> LaserControllers {
            set { laserControllers=value; }
            get { return laserControllers; }
        }
        #endregion

    }
    #endregion

    #region Send Laser Points
    [Serializable]
    public class SendBlankPointPacket : NetworkPacket {

        #region Fields
        private string deviceAddress;
        private double x;
        private double y;
        private UInt32 deviceScanrate;
        #endregion

        #region Lifecycle
        public SendBlankPointPacket() : base() { }
        #endregion

        #region Properties
        public string DeviceAddress {
            set { deviceAddress=value; }
            get { return deviceAddress; }
        }

        public double X {
            set { x=value; }
            get { return x; }
        }

        public double Y {
            set { y=value; }
            get { return y; }
        }

        public UInt32 DeviceScanrate {
            set { deviceScanrate=value; }
            get { return deviceScanrate; }
        }
        #endregion

    }

    [Serializable]
    public class SendLaserPointPacket : NetworkPacket {

        #region Fields
        private string deviceAddress;
        private UInt32 deviceScanrate;
        private List<LaserPoint> laserPoints;
        #endregion

        #region Lifecycle
        public SendLaserPointPacket() : base() {
            laserPoints=new List<LaserPoint>();
        }
        #endregion

        #region Properties
        public string DeviceAddress {
            set { deviceAddress=value; }
            get { return deviceAddress; }
        }

        public UInt32 DeviceScanrate {
            set { deviceScanrate=value; }
            get { return deviceScanrate; }
        }

        public List<LaserPoint> LaserPoints {
            set { laserPoints=value; }
            get { return laserPoints; }
        }
        #endregion

    }
    #endregion

    #region Laser Controller
    [Serializable]
    public class LaserController {

        private UInt32 index = 0;
        private string address = "";
        private UInt32 scanrate;
        private UInt32 maxScanrate;
        private UInt32 minScanrate;
        private UInt32 maxNumOfPoints;
        private string deviceType = "";

        private bool failed = false;
        private bool invalid = false;
        private bool overflow = false;
        private bool exceeded = false;
        private bool undefined = false;

        private int errorCode = 0;
        private string errorMessage = "OK";

        private LaserPoint mostRecentPoint;

        public LaserController() { }

        public UInt32 Index {
            set { index=value; }
            get { return index; }
        }
        public string Address {
            set { address=value; }
            get { return address; }
        }
        public UInt32 Scanrate {
            set { scanrate=value; }
            get { return scanrate; }
        }
        public UInt32 MaxScanrate {
            set { maxScanrate=value; }
            get { return maxScanrate; }
        }
        public UInt32 MinScanrate {
            set { minScanrate=value; }
            get { return minScanrate; }
        }
        public UInt32 MaxNumOfPoints {
            set { maxNumOfPoints=value; }
            get { return maxNumOfPoints; }
        }
        public string DeviceType {
            set { deviceType=value; }
            get { return deviceType; }
        }

        public bool Failed {
            set { failed=value; }
            get { return failed; }
        }

        public bool Invalid {
            set { invalid=value; }
            get { return invalid; }
        }

        public bool Overflow {
            set { overflow=value; }
            get { return overflow; }
        }

        public bool Exceeded {
            set { exceeded=value; }
            get { return exceeded; }
        }

        public bool Undefined {
            set { undefined=value; }
            get { return undefined; }
        }

        public int ErrorCode {
            set { errorCode=value; }
            get { return errorCode; }
        }

        public string ErrorMessage {
            set { errorMessage=value; }
            get { return errorMessage; }
        }

        public LaserPoint MostRecentPoint {
            set { mostRecentPoint=value; }
            get { return mostRecentPoint; }
        }

    }
    #endregion

    #region Laser Device
    [Serializable]
    public class LaserDevice {

        private string address;

        private LaserPoint interFrameBlankPoint;                        // cache last frame from previous frame to interpolate blanking points

        private double distanceWithOptimization = 0.0;                  // used to measure optimisation efficiency
        private double distanceWithoutOptimization = 0.0;               // used to measure optimisation efficiency

        private List<LaserShape> shapes = new List<LaserShape>();

        public LaserDevice() { }

        public string Address {
            set { address=value; }
            get { return address; }
        }

        public List<LaserShape> Shapes {
            set { shapes=value; }
            get { return shapes; }
        }

        public LaserPoint InterFrameBlankPoint {
            set { interFrameBlankPoint=value; }
            get { return interFrameBlankPoint; }
        }

        public double DistanceWithOptimization {
            set { distanceWithOptimization=value; }
            get { return distanceWithOptimization; }
        }

        public double DistanceWithoutOptimization {
            set { distanceWithoutOptimization=value; }
            get { return distanceWithoutOptimization; }
        }

        public double DistanceSavings {
            get {
                if(distanceWithoutOptimization!=0.0) {
                    return distanceWithOptimization/distanceWithoutOptimization;
                }
                return 1.0;
            }
        }

    }
    #endregion

    #region Laser Shape
    [Serializable]
    public class LaserShape {

        private List<LaserPoint> points = new List<LaserPoint>();

        public LaserShape() { }

        public LaserPoint StartPoint {
            get {
                return points[0];
            }
        }

        public LaserPoint EndPoint {
            get {
                return points[points.Count-1];
            }
        }

        public List<LaserPoint> Points {
            set { points=value; }
            get { return points; }
        }

        public void addRepetitionPoints(bool start, int amount) {
            if(points.Count>0) {
                if(start) {
                    List<LaserPoint> lightPoints = new List<LaserPoint>();
                    for(int i = 0; i<amount; i++) {
                        LaserPoint point = new LaserPoint();
                        point.X = points[0].X;
                        point.Y = points[0].Y;
                        point.Colors = points[0].Colors;
                        lightPoints.Add(point);
                    }
                    lightPoints.AddRange(points);
                    points = lightPoints;
                } else {
                    for(int i = 0; i<amount; i++) {
                        LaserPoint point = new LaserPoint();
                        point.X = points[points.Count-1].X;
                        point.Y = points[points.Count-1].Y;
                        point.Colors = points[points.Count-1].Colors;
                        points.Add(point);
                    }
                }
            }
        }

        public LaserShape copy() {
            LaserShape instance = new LaserShape();
            foreach(LaserPoint point in points) {
                instance.points.Add(point.copy());
            }
            return instance;
        }

    }
    #endregion

    #region Laser Point
    [Serializable]
    public class LaserPoint {

        private double x;
        private double y;
        private byte[] colors;

        public LaserPoint() { }

        public double X {
            set { x=value; }
            get { return x; }
        }

        public double Y {
            set { y=value; }
            get { return y; }
        }

        public byte[] Colors {
            set { colors=value; }
            get { return colors; }
        }

        public LaserPoint copy() {
            LaserPoint instance = new LaserPoint();
            instance.x = x;
            instance.y = y;
            instance.colors = new byte[] { colors[0], colors[1], colors[2], colors[3], colors[4], colors[5] };
            return instance;
        }

    }
    #endregion

}