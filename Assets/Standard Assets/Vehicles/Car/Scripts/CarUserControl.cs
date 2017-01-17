using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace UnityStandardAssets.Vehicles.Car
{
    [RequireComponent(typeof(CarController))]
    public class CarUserControl : MonoBehaviour
    {
        public Rigidbody EnemyCar;
        public GameObject m_SpeedoMeterPointer;
        public UnityEngine.UI.Text m_TimeText, m_CountText, m_InfoText;

        private const int SEND_PERIOD = 50, PLAYER_NUM = 1;

        private Rigidbody[] m_Bodys;
        private CarController[] m_Controllers;

        private Thread m_Thread, m_PositionThread;
        private DateTime startTime;
        private string m_Text = "", m_TextInfo = "";
        private int m_Id = -1;
        private float[] m_PlayerHorizontals;
        private int[] m_PlayerVerticals;
        private byte[] m_PosAndRot, m_EnemyPosAndRot;
        private bool m_Run = false, m_Change = false, m_SendPositionUpdate = false, m_SendPosition = false, m_EnemyPositionUpdate = false, m_Start = false, m_Finish = false, m_SendFinish = false;

        public void Start()
        {
            m_PosAndRot = new byte[26];
            m_PosAndRot[0] = 1;
            m_EnemyPosAndRot = new byte[25];

            m_Bodys = new Rigidbody[PLAYER_NUM];
            m_Controllers = new CarController[PLAYER_NUM];
            m_PlayerHorizontals = new float[PLAYER_NUM];
            m_PlayerVerticals = new int[PLAYER_NUM];

            m_Thread = new Thread(ThreadListener);
            m_Thread.Start();
        }

        private void OnDisable()
        {
            m_Run = false;
            m_Thread.Join();
            if (m_PositionThread.ThreadState == ThreadState.Running)
                m_PositionThread.Join();
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.tag == "finish" && !m_Finish)
            {
                m_Text = "Win!";
                m_SendFinish = true;
                GameEndHandle();
            }
        }

        private void Update()
        {
            if (m_Change)
            {
                float x = -797, y = 5, z = 1450, xr = 0, yr = 35, zr = 0;
                for (int i = 0; i < PLAYER_NUM; i++)
                {
                    if (m_Id == i)
                    {
                        m_Bodys[i] = GetComponent<Rigidbody>();
                        m_Controllers[i] = GetComponent<CarController>();
                        m_Bodys[i].transform.Translate(new Vector3(-5, 0, -2) * i);
                    }
                    else
                    {
                        m_Bodys[i] = Instantiate(EnemyCar, new Vector3(x, y, z) - new Vector3(-5, 0, -2) * i, Quaternion.Euler(new Vector3(xr, yr, zr)));
                        m_Controllers[i] = m_Bodys[i].GetComponent<CarController>();
                    }
                }
                m_Change = false;
            }

            if (m_Start)
            {
                if (m_EnemyPositionUpdate)
                {
                    int id = m_EnemyPosAndRot[24];
                    m_Bodys[id].transform.position = new Vector3(BitConverter.ToSingle(m_EnemyPosAndRot, 0), BitConverter.ToSingle(m_EnemyPosAndRot, 4), BitConverter.ToSingle(m_EnemyPosAndRot, 8));
                    m_Bodys[id].transform.Rotate(new Vector3(BitConverter.ToSingle(m_EnemyPosAndRot, 12), BitConverter.ToSingle(m_EnemyPosAndRot, 16), BitConverter.ToSingle(m_EnemyPosAndRot, 20))
                        - m_Bodys[id].transform.rotation.eulerAngles);
                    m_EnemyPositionUpdate = false;
                }

                if (m_SendPositionUpdate)
                {
                    byte[][] arrays = new byte[6][];
                    arrays[0] = BitConverter.GetBytes(m_Bodys[m_Id].transform.position.x);
                    arrays[1] = BitConverter.GetBytes(m_Bodys[m_Id].transform.position.y);
                    arrays[2] = BitConverter.GetBytes(m_Bodys[m_Id].transform.position.z);
                    arrays[3] = BitConverter.GetBytes(m_Bodys[m_Id].transform.rotation.eulerAngles.x);
                    arrays[4] = BitConverter.GetBytes(m_Bodys[m_Id].transform.rotation.eulerAngles.y);
                    arrays[5] = BitConverter.GetBytes(m_Bodys[m_Id].transform.rotation.eulerAngles.z);

                    for (int i = 0; i < 6; i++)
                        for (int j = 0; j < 4; j++)
                            m_PosAndRot[1 + i * 4 + j] = arrays[i][j];
                    m_PosAndRot[25] = (byte)m_Id;
                    m_SendPosition = true;
                }

                float factor = m_Controllers[m_Id].CurrentSpeed / m_Controllers[m_Id].MaxSpeed;
                float angle;
                if (m_Controllers[m_Id].CurrentSpeed >= 0)
                    angle = Mathf.Lerp(0, 180, factor);
                else
                    angle = Mathf.Lerp(0, 180, -factor);
                m_SpeedoMeterPointer.transform.Rotate(new Vector3(0, 0, -angle - m_SpeedoMeterPointer.transform.rotation.eulerAngles.z));

                TimeSpan time = DateTime.Now - startTime;
                m_TimeText.text = string.Format("{0:D2}:{1:D2}:{2:D3}", time.Minutes, time.Seconds, time.Milliseconds);
            }

            m_CountText.text = m_Text;
            m_InfoText.text = m_TextInfo;
        }

        private void FixedUpdate()
        {
            if (m_Start)
            {
                for (int i = 0; i < PLAYER_NUM; i++)
                    m_Controllers[i].Move(m_PlayerHorizontals[i], m_PlayerVerticals[i], m_PlayerVerticals[i], 0f);
            }
        }

        private void SendPositionListener()
        {
            while (m_Run)
            {
                m_SendPositionUpdate = true;
                Thread.Sleep(SEND_PERIOD);
            }
        }

        private void GameStartListener()
        {
            m_Text = "3";
            Thread.Sleep(1000);
            m_Text = "2";
            Thread.Sleep(1000);
            m_Text = "1";
            Thread.Sleep(1000);
            m_Text = "Go!";
            m_TextInfo = "";
            startTime = DateTime.Now;
            m_Start = true;
            m_PositionThread = new Thread(SendPositionListener);
            m_PositionThread.Start();
            Thread.Sleep(1000);
            m_Text = "";
        }

        private void GameEndHandle()
        {
            m_Finish = true;
            m_Run = false;
            m_PositionThread.Join();
            m_Thread.Join();
            m_TextInfo = "The Game will be restarted automatically after 5 seconds.";

            Thread.Sleep(5000);
            m_Thread = new Thread(ThreadListener);
            m_Thread.Start();
        }


        private void ThreadListener()
        {
            TcpClient client = new TcpClient();
            NetworkStream ns = null;
            int port = 3000;

            try
            {
                client.Connect(new IPEndPoint(IPAddress.Parse("52.78.108.211"), port));
                ns = client.GetStream();

                byte[] data = new byte[10];

                data[0] = 1;
                ns.Write(data, 0, data.Length);
                ns.Flush();

                ns.Read(data, 0, 1);
                m_Id = data[0];
                m_TextInfo = "Your pad ID is " + m_Id;

                m_Change = true;
                m_Run = true;

                while (m_Run)
                {
                    ns.Read(data, 0, 1);
                    Debug.Log(data[0]);
                    if (data[0] == 0)
                    {
                        ns.Read(data, 0, 2);
                        int id = data[1] >> 4;
                        if (id < PLAYER_NUM)
                        {
                            m_PlayerHorizontals[id] = 1 - ((float)data[0]) / 128;

                            if ((data[1] & 1) == 1)
                                m_PlayerVerticals[id] = 1;
                            else if ((data[1] & 2) == 2)
                                m_PlayerVerticals[id] = -1;
                            else
                                m_PlayerVerticals[id] = 0;
                        }
                    }
                    else if (data[0] == 1)
                    {
                        ns.Read(m_EnemyPosAndRot, 0, 25);
                        m_EnemyPositionUpdate = true;
                    }
                    else
                    {
                        if (m_Start)
                        {
                            if (!m_Finish)
                            {
                                m_Text = "Lose";
                                GameEndHandle();
                            }
                        }
                        else
                        {
                            new Thread(GameStartListener).Start();
                        }
                    }

                    if (m_SendPosition)
                    {
                        ns.Write(m_PosAndRot, 0, m_PosAndRot.Length);
                        ns.Flush();
                        m_SendPosition = false;
                    }

                    if (m_Finish && m_SendFinish)
                    {
                        data[0] = 255;
                        ns.Write(data, 0, data.Length);
                        ns.Flush();
                        m_SendFinish = false;
                    }
                }
            }
            catch (InvalidOperationException InvOpEx)
            {
                Debug.Log("TCP exception: " + InvOpEx.Message);
            }
            catch (SocketException SockEx)
            {
                Debug.Log("Socket exception: " + SockEx.Message);
            }
            finally
            {
                if (ns != null)
                    ns.Close();
                client.Close();
            }
        }
    }
}