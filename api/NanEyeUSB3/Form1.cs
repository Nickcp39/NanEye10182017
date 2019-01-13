///For the NanoUSB3 version 1.0 uncomment the following file
//#define NanoUSB3_v1_0

///For the NanoUSB3 version 1.1 uncomment the following file
#define NanoUSB3_v1_1

///In case to save videos, uncoment one of the following lines
//#define SaveAwVideo //saves raw video
#define SaveAviVideo //saves processed video

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Awaiba.Drivers.Grabbers;
using Awaiba.FrameProcessing;
using Awaiba.Algorithms;
using System.Drawing.Imaging;
using System.Threading;
using System.Diagnostics;
using Awaiba.Media;
using System.IO;

using System.Net;
using System.Net.Sockets;

//TODO: Add the events for the color reconstruction checkbox for the J2,J3 and J4
//TODO: Put AEC option at checked when starting the form

namespace NanEyeUSB3Demo
{
    public partial class Form1 : Form
    {
        //Create the Provider for the v1.1 of the Adapter
#if(NanoUSB3_v1_1)
        NanEye2DUSB3_2Provider provider = new NanEye2DUSB3_2Provider();
#endif

        //Create the Provider for the v1.0 of the Adapter
#if(NanoUSB3_v1_0)
            NanEye2DUSB3Provider provider = new NanEye2DUSB3Provider();
#endif

        //Creates the list of AEC instances that will handle the automatic exposure control algorithm
        //List<AutomaticExposureControlHardware> aec = new List<AutomaticExposureControlHardware>();

#if(SaveAwVideo)
            AwRawVideo video;

#endif

#if(SaveAviVideo)
        AwVideo video;
#endif

        int imgI = 0;
        int pre_mark_num = 0; // mark
        int mark_num = 0; // mark
        bool quit = false;
        IPAddress ipAd;
        // use local m/c IP address, and 
        // use the same in the client
       
        /* Initializes the Listener */
        TcpListener myList;
        Socket s;
        bool connected = false;

        private void labviewConnect()
        {
            
            Console.WriteLine("Starting LabviewConnect thread");
            byte[] b = new byte[100];
            byte[] c = new byte[100];
            int k = 0;
            Console.WriteLine("Waiting for a connection.....");
            string baseFolder = "";
            
            while (!quit)
            {
                
                if (myList.Pending())
                {
                    s = myList.AcceptSocket();
                    Console.WriteLine("Connection accepted from " + s.RemoteEndPoint);
                    connected = true;
                    baseFolder = folderNameBox.Text + "/";
                    if (folderNameBox.Text == "")
                    {
                        Console.WriteLine("Folder name is empty, using default as folder name");
                        baseFolder = "default/";
                    }
                    //creat the base floder when folder name not exist. 
                    if (!Directory.Exists(baseFolder))
                    {
                        Console.WriteLine("Created " + baseFolder + "folder");
                        Directory.CreateDirectory(baseFolder);
                        Console.WriteLine("Created Laser1 folder");
                        Directory.CreateDirectory(baseFolder + "Laser1");
                        Console.WriteLine("Created Laser2 folder");
                        Directory.CreateDirectory(baseFolder + "Laser2");
                    }
                    //intial image number
                    imgI = 0;
                    while (!quit && connected)
                    {
                        Console.WriteLine("Waiting to recieve.");
                        k = s.Receive(b);
                        Console.WriteLine(DateTime.Now.ToString("mm:ss:fff tt"));
                        Console.WriteLine("Recieved...");
                        for (int i = 0; i < k; i++)
                            Console.Write(Convert.ToChar(b[i]));
                        Console.WriteLine("");

                        // when trigger recieve and included software trigger
                        if ((Convert.ToChar(b[0]).Equals('T')) && (imgCapBool == true))
                        {
                            imgI++;
                            int imagenum = imgI / 2;
                            string folder = "";
                            string lasernum =  Convert.ToChar(b[1]).ToString();
                            folder = String.Concat("Laser", lasernum, "/");
                            //take the snapshot 
                            ProcessingWrapper.pr[0].TakeSnapshost().processedImage.Save(baseFolder + folder + "Laser" + lasernum + "image" + Convert.ToString(imagenum) + ".png");
                            Console.WriteLine("Saving snapshot " + Convert.ToString(imgI) + " to " + baseFolder + folder);

                            try
                            {
                            //    ProcessingWrapper.pr[0].TakeSnapshost().rawImage.Save(baseFolder + folder + "Laser" + lasernum + "image" + Convert.ToString(imagenum) + ".pgm");

                            }
                            catch (Exception e)
                            {
                                Console.WriteLine("Error..... " + e.StackTrace);
                            }

                        }
                        else if (Convert.ToChar(b[0]).Equals('Q'))
                        {
                            connected = false;
                        }
                        else if (!Convert.ToChar(b[0]).Equals('T'))
                        {
                            Console.WriteLine("Recieved something other than trigger or quit");
                        }
                        
                    }
                    Console.WriteLine("Connection lost!");
                    Console.WriteLine("Waiting for a connection.....");
                }
                Thread.Sleep(500);
            }
            Console.WriteLine("Quitting LabviewConnect thread");
            if (s != null)
            {
                s.Send(Encoding.ASCII.GetBytes("Quit"));
                s.Close();
            }
            
            myList.Stop();
        }
        
        public Form1()
        {
            InitializeComponent();

            ///To program the FPGA with the bin file and the FW file
            ///You can choose the folder/file combination where the files are

            ///For the v1.1 of the Adapter
#if(NanoUSB3_v1_1)
            provider.SetFpgaFile(@"Fpga Files\NanEye_efm02 - XC6SLX45 v2.2.0.bin");
            provider.SetFWFile(@"Fpga Files\fx3_fw_2EP.img");
#endif

            ///For the v1.0 of the Adapter
#if(NanoUSB3_v1_0)
                provider.SetFpgaFile(@"Fpga Files\NanEye_efm02 - XC6SLX45 v1.0.0.bin");
                provider.SetFWFile(@"Fpga Files\fx3_fw_30EP.img");
#endif
            /*** To initialize the sensors in the correct state:
            ///If you want to receive data from it, please put it at true, else, put it at false
            ///The sensors are organized in the code as in the Documentation, regarding the J1 to J4 connectors ***/
            //Create the list of sensors to get data from
            List<bool> sensorsReceive = new List<bool>();
            sensorsReceive.Add(true);               //J1
            sensorsReceive.Add(false);               //J2
            sensorsReceive.Add(false);               //J3
            sensorsReceive.Add(false);               //J4
            provider.Sensors = sensorsReceive;


            /*** For the Automatic Exposure Control ***/
            ///Need to create fours instances, one for each connector
            ///Use the one that your sensor is plugged
            ///For more information please check the documentation on Awaiba's website
            //for(int i=0; i<4; i++)
            //{
            //    aec.Add(new AutomaticExposureControlHardware());
            //    aec[i].SensorId = i;
            //}

            //provider.SetAutomaticExpControl(aec);
#if(SaveAwVideo)
                video = new AwRawVideo(2045, 8);
#endif

#if(SaveAviVideo)
            video = new AwVideo();
#endif
            /*** Event Handlers to get the image and handle the exceptions from the bottom layers ***/
            provider.ImageProcessed += provider_ImageProcessed;
            provider.Exception += provider_Exception;
            try
            {
                /* Start Listening at the specified port */
                ipAd = IPAddress.Parse("127.0.0.1");
                myList = new TcpListener(ipAd, 8001);
                myList.Start();

                Console.WriteLine("The server is running at port 8001...");
                Console.WriteLine("The local End point is  :" +
                    myList.LocalEndpoint);
                System.Threading.Thread newThread = new Thread(labviewConnect);
                newThread.Name = "LabviewConnect";
                newThread.IsBackground = true;
                newThread.Start();
            } 
            catch (Exception e)
            {
                Console.WriteLine("Error..... " + e.StackTrace);
                return;
            }
        }

        /// <summary>
        /// This event is triggered when there is some error in the bottom layers
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void provider_Exception(object sender, OnExceptionEventArgs e)
        {
            Console.WriteLine(e.ex.Message);
        }

        private void provider_ImageProcessed(object sender, OnImageReceivedBitmapEventArgs e)
        {
            //Handle the image data
            if (e.SensorID == 0)
            {
                pictureBox1.Image = ProcessingWrapper.pr[0].CreateProcessedBitmap(e.GetImageData);
                /*if (imgCapBool) 
                {
                    //ProcessingWrapper.pr[0].TakeSnapshost().processedImage.Save("image" + (++imgI).ToString() + ".png");
                    pictureBox1.Image.Save("output" + imgI.ToString() + ".bmp");
                }*/

            }
            else if (e.SensorID == 1)
                pictureBox2.Image = ProcessingWrapper.pr[1].CreateProcessedBitmap(e.GetImageData);
            else if (e.SensorID == 2)
                pictureBox3.Image = ProcessingWrapper.pr[2].CreateProcessedBitmap(e.GetImageData);
            else if (e.SensorID == 3)
                pictureBox4.Image = ProcessingWrapper.pr[3].CreateProcessedBitmap(e.GetImageData);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            provider.StopCapture();
            imgCapBool = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Console.WriteLine("Starting capture");
            provider.StartCapture();
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            try
            {
                label9.Text = trackBar1.Value.ToString();
                provider.WriteRegister(new NanEyeUSB3RegisterPayload(0x01, true, 0, trackBar1.Value));

            }
            catch { }
        }

        private void trackBar2_Scroll(object sender, EventArgs e)
        {
            try
            {
                provider.WriteRegister(new NanEyeUSB3RegisterPayload(0x01, true, 1, trackBar2.Value));
            }
            catch { }
        }

        private void trackBar3_Scroll(object sender, EventArgs e)
        {
            try
            {
                provider.WriteRegister(new NanEyeUSB3RegisterPayload(0x01, true, 2, trackBar3.Value));
            }
            catch { }
        }

        private void trackBar4_Scroll(object sender, EventArgs e)
        {
            try
            {
                provider.WriteRegister(new NanEyeUSB3RegisterPayload(0x01, true, 3, trackBar4.Value));
            }
            catch { }
        }

        private void trackBar5_Scroll(object sender, EventArgs e)
        {
            try
            {
                label10.Text = trackBar5.Value.ToString();
                provider.WriteRegister(new NanEyeUSB3RegisterPayload(0x02, true, 0, trackBar5.Value));

            }
            catch { }
        }

        private void trackBar6_Scroll(object sender, EventArgs e)
        {
            try
            {
                label11.Text = trackBar6.Value.ToString();
                provider.WriteRegister(new NanEyeUSB3RegisterPayload(0x03, true, 0, trackBar6.Value));

            }
            catch { }
        }

        private void trackBar7_Scroll(object sender, EventArgs e)
        {
            try
            {
                label12.Text = trackBar7.Value.ToString();
                provider.WriteRegister(new NanEyeUSB3RegisterPayload(0x07, true, 0, trackBar7.Value));

            }
            catch { }
        }

        private void trackBar8_Scroll(object sender, EventArgs e)
        {
            try
            {
                label13.Text = trackBar8.Value.ToString();
                provider.WriteRegister(new NanEyeUSB3RegisterPayload(0x08, true, 0, trackBar8.Value));

            }
            catch { }
        }

        private void trackBar9_Scroll(object sender, EventArgs e)
        {
            try
            {
                label15.Text = trackBar9.Value.ToString();
                provider.WriteRegister(new NanEyeUSB3RegisterPayload(0x04, true, 0, trackBar9.Value));

            }
            catch { }

        }


        private void checkBox8_CheckedChanged(object sender, EventArgs e)
        {
            ProcessingWrapper.pr[3].colorReconstruction.Apply = checkBox8.Checked;
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            ProcessingWrapper.pr[1].colorReconstruction.Apply = checkBox10.Checked;
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            ProcessingWrapper.pr[2].colorReconstruction.Apply = checkBox12.Checked;
        }

        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            //ProcessingWrapper.pr[0].colorReconstruction.Apply = checkBox1.Checked;

            if (checkBox1.Checked)
            {
#if(SaveAviVideo)
                video.StartVideo(45, "Testing.avi", false);
#endif

#if(SaveAwVideo)
                    video.StartVideo("Testing.awvideo");
#endif

                provider.ImageProcessed += SaveVideoFrame;
                //ProcessingWrapper.pr[0].TakeSnapshost().processedImage.Save("image" + (++imgI).ToString() + ".png");


                // Sets the timer interval to 1 seconds.
                timer1.Interval = 250; // 1 second 4 times
                //1000; // =100; // 0.1 seconds
                //timer1.Start();

                // Runs the timer, and raises the event.
                while (exitFlag == false)
                {
                    // Processes all the events in the queue.
                    Application.DoEvents();
                }
            }
            else
            {
                provider.ImageProcessed -= SaveVideoFrame;

#if(SaveAviVideo)
                video.StopVideo();
#endif

#if(SaveAwVideo)
                    video.Close();
#endif

                //timer1.Stop();
                exitFlag = true;
            }
        }

        bool locker = false;
        private void SaveVideoFrame(object sender, OnImageReceivedBitmapEventArgs e)
        {
            if (locker)
                return;

            locker = true;

#if(SaveAviVideo)
            video.AddFrame(ProcessingWrapper.pr[e.SensorID].CreateProcessedBitmap(e.GetImageData));
#endif

#if(SaveAwVideo)
            Awaiba.Imaging.PGMImage pgm1 = Awaiba.Imaging.PGMImage.FromFile("all.pgm");
            //Awaiba.Imaging.PGMImage pgm2 = Awaiba.Imaging.PGMImage.FromFile("red.pgm");
            video.AddFrame(pgm1);
           // video.AddFrame(pgm2);
#endif

            locker = false;
        }

        #region Automatic Exposure Control enable/disable
        private void checkBox5_CheckedChanged(object sender, EventArgs e)
        {
            provider.AutomaticExpControl(0).aec.Enabled = 1 - Convert.ToInt32(checkBox5.Checked);
        }

        private void checkBox9_CheckedChanged(object sender, EventArgs e)
        {
            provider.AutomaticExpControl(1).aec.Enabled = 1 - Convert.ToInt32(checkBox9.Checked);
        }

        private void checkBox11_CheckedChanged(object sender, EventArgs e)
        {
            provider.AutomaticExpControl(2).aec.Enabled = 1 - Convert.ToInt32(checkBox11.Checked);
        }

        private void checkBox7_CheckedChanged(object sender, EventArgs e)
        {
            provider.AutomaticExpControl(3).aec.Enabled = 1 - Convert.ToInt32(checkBox7.Checked);
        }
        #endregion

        bool imgCapBool = false;

        private void checkBox2_CheckedChanged_1(object sender, EventArgs e)
        {
            if (checkBox2.CheckState.Equals(CheckState.Checked))
            {
                imgCapBool = true;
            }
            else
            {
                imgCapBool = false;
            }
        }


        int alarmCounter = 1;
        bool exitFlag = false;
        private void timer1_Tick(object sender, EventArgs e)
        {
            //timer1.Stop();          
            // Restarts the timer and increments the counter.
            alarmCounter += 1;
            timer1.Enabled = true;
            //ProcessingWrapper.pr[0].TakeSnapshost().processedImage.Save("image" + (imgI++).ToString() + ".png");
            if (pre_mark_num == mark_num)
                ProcessingWrapper.pr[0].TakeSnapshost().rawImage.Save("raw_image" + (imgI++).ToString() + ".pgm");
            else
                ProcessingWrapper.pr[0].TakeSnapshost().rawImage.Save("raw_image" + (imgI++).ToString() + "_Mark" + mark_num.ToString() + ".pgm");
            pre_mark_num = mark_num;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            mark_num++;

        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            /* clean up */
            quit = true;
        }

        private void folderNameBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void label16_Click(object sender, EventArgs e)
        {

        }

    }
}