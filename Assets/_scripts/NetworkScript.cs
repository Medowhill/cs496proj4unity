using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;
using UnityStandardAssets.Vehicles.Car;

public class NetworkScript : MonoBehaviour
{
    public Rigidbody MyCar, EnemyCar, Bullet;
    public GameObject Road;
    public Vector3 m_Position, m_Rotation;

    private const int SEND_PERIOD = 20, PLAYER_NUM = 1, REVERSE_TIME = 5000, HIDE_TIME = 5000, BOOST_FACTOR = 50000, BULLET_FACTOR = 250000, START_BOOST_FACTOR = 500;

    private GameObject m_Road;
    private Rigidbody[] m_Bodys;
    private CarController[] m_Controllers;
    private CarUIScript m_CarUI;

    private object m_Lock = new object();
    private Thread m_Thread, m_PositionThread;
    private DateTime m_StartTime, m_StopTime;
    private System.Random m_Random;
    private HashSet<GameObject> m_BadItemSet;
    private int m_Id, m_Item, m_Checkpoint;
    private Vector3 m_CheckpointPosition, m_CheckpointRotation;
    private float[] m_PlayerHorizontals;
    private int[] m_PlayerVerticals, m_PlayerHandBrakes;
    private byte[] m_PlayerStartBoosts;
    private bool[] m_PlayerReversed;
    private byte[] m_PosAndRot, m_EnemyPosAndRot, m_ItemInfo, m_PlayerItemInfo;
    private bool m_Run, m_Change, m_SendPositionUpdate, m_SendPosition, m_EnemyPositionUpdate, m_Start, m_Finish, m_SendFinish, m_Destroy, m_SendItem,
        m_PlayerItemUpdate, m_UseItem, m_ReverseSend, m_ReverseBoost, m_StartBoost;

    public void Start()
    {
        m_Random = new System.Random();
        Initialize();
    }

    private void Initialize()
    {
        m_PosAndRot = new byte[26];
        m_PosAndRot[0] = 1;
        m_EnemyPosAndRot = new byte[25];

        m_ItemInfo = new byte[15];
        m_ItemInfo[0] = 2;
        m_PlayerItemInfo = new byte[14];

        m_Bodys = new Rigidbody[PLAYER_NUM];
        m_Controllers = new CarController[PLAYER_NUM];
        m_PlayerHorizontals = new float[PLAYER_NUM];
        m_PlayerVerticals = new int[PLAYER_NUM];
        m_PlayerHandBrakes = new int[PLAYER_NUM];
        m_PlayerReversed = new bool[PLAYER_NUM];
        m_PlayerStartBoosts = new byte[PLAYER_NUM];

        m_Run = m_Change = m_SendPositionUpdate = m_SendPosition = m_EnemyPositionUpdate = m_Start = m_Finish = m_SendFinish = m_Destroy = m_SendItem
                = m_PlayerItemUpdate = m_UseItem = m_ReverseSend = m_ReverseBoost = m_StartBoost = false;
        m_Id = m_Item = m_Checkpoint = -1;

        m_Bodys[0] = Instantiate(MyCar, new Vector3(0, 0, 10), Quaternion.identity);
        m_CarUI = m_Bodys[0].GetComponent<CarUIScript>();
        m_CarUI.setTriggerExit(TriggerExit);
        m_CarUI.setTriggerEnter(TriggerEnter);

        m_Road = Instantiate(Road, new Vector3(0, 0, 0), Quaternion.identity);
        m_Thread = new Thread(ThreadListener);
        m_Thread.Start();
    }

    private void OnDisable()
    {
        m_Run = false;
        m_Thread.Join();
        if (m_PositionThread != null && m_PositionThread.ThreadState == ThreadState.Running)
            m_PositionThread.Join();
    }

    private void TriggerEnter(Collider other)
    {
        if (other.tag == "item")
        {
            Destroy(other.gameObject);
            m_Item = m_Random.Next(0, 3);
        }
        else if (other.tag == "baditem")
        {
            if (m_BadItemSet.Contains(other.gameObject))
                return;

            Destroy(other.gameObject);
            m_BadItemSet.Add(other.gameObject);
            if (m_Item == 2)
            {
                m_Item = -1;
            }
            else
            {
                int item = m_Random.Next(0, 3);
                switch (item)
                {
                    case 0:
                        m_ReverseSend = true;
                        break;
                    case 1:
                        m_ReverseBoost = true;
                        break;
                    case 2:
                        m_CarUI.setHide(true);
                        new Thread(() =>
                        {
                            Thread.Sleep(HIDE_TIME);
                            m_CarUI.setHide(false);
                        }).Start();
                        break;
                }
            }
        }
    }

    private void TriggerExit(Collider other)
    {
        if (other.tag == "checkpoint")
        {
            int index = other.gameObject.name.ToCharArray()[0] - '0';
            if (m_Checkpoint < index)
            {
                m_Checkpoint = index;
                m_CheckpointPosition = m_Bodys[m_Id].transform.position;
                m_CheckpointRotation = m_Bodys[m_Id].transform.rotation.eulerAngles;
            }
        }
        else if (other.tag == "finish" && !m_Finish && m_Run)
        {
            m_CarUI.setText("Win!");
            m_SendFinish = true;
        }
    }

    private void Update()
    {
        if (m_Change)
        {
            m_Change = false;
            m_Bodys[m_Id] = m_Bodys[0];
            for (int i = 0; i < PLAYER_NUM; i++)
            {
                if (m_Id == i)
                {
                    m_CarUI.setInfoText("Your pad ID is " + i);
                    m_Bodys[i].position = m_Position + new Vector3(-5, 0, -2) * i;
                    m_Bodys[i].rotation = Quaternion.Euler(m_Rotation);
                }
                else
                {
                    m_Bodys[i] = Instantiate(EnemyCar, m_Position + new Vector3(-5, 0, -2) * i, Quaternion.Euler(m_Rotation));
                }
                m_Controllers[i] = m_Bodys[i].GetComponent<CarController>();
            }
        }

        if (m_Start)
        {
            DateTime now = DateTime.Now;
            if (m_Bodys[m_Id].velocity.magnitude > 1)
                m_StopTime = now;
            else if ((now - m_StopTime).TotalSeconds >= 5)
            {
                m_StopTime = now;
                if (m_Checkpoint > -1)
                {
                    m_Bodys[m_Id].transform.position = m_CheckpointPosition;
                    m_Bodys[m_Id].transform.rotation = Quaternion.Euler(m_CheckpointRotation);
                }
            }

            if (m_EnemyPositionUpdate)
            {
                int id = m_EnemyPosAndRot[m_EnemyPosAndRot.Length - 1];
                if (id != m_Id)
                {
                    m_Bodys[id].transform.position = new Vector3(BitConverter.ToSingle(m_EnemyPosAndRot, 0), BitConverter.ToSingle(m_EnemyPosAndRot, 4), BitConverter.ToSingle(m_EnemyPosAndRot, 8));
                    m_Bodys[id].transform.Rotate(new Vector3(BitConverter.ToSingle(m_EnemyPosAndRot, 12), BitConverter.ToSingle(m_EnemyPosAndRot, 16), BitConverter.ToSingle(m_EnemyPosAndRot, 20))
                        - m_Bodys[id].transform.rotation.eulerAngles);
                    m_EnemyPositionUpdate = false;
                }
            }

            if (m_Bodys[m_Id].transform.position.y < -20)
            {
                if (m_Checkpoint > -1)
                {
                    m_Bodys[m_Id].transform.position = m_CheckpointPosition;
                    m_Bodys[m_Id].transform.rotation = Quaternion.Euler(m_CheckpointRotation);
                }
            }

            if (m_UseItem && (m_Item == 0 || m_Item == 1))
            {
                m_UseItem = false;

                byte[][] arrays = new byte[3][];
                arrays[0] = BitConverter.GetBytes(m_Bodys[m_Id].transform.forward.x);
                arrays[1] = BitConverter.GetBytes(m_Bodys[m_Id].transform.forward.y);
                arrays[2] = BitConverter.GetBytes(m_Bodys[m_Id].transform.forward.z);
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 4; j++)
                        m_ItemInfo[1 + i * 4 + j] = arrays[i][j];
                m_ItemInfo[m_ItemInfo.Length - 2] = (byte)m_Item;
                m_ItemInfo[m_ItemInfo.Length - 1] = (byte)m_Id;

                m_Item = -1;
                m_SendItem = true;
            }

            if (m_ReverseBoost)
            {
                m_ReverseBoost = false;

                byte[][] arrays = new byte[3][];
                arrays[0] = BitConverter.GetBytes(-m_Bodys[m_Id].transform.forward.x);
                arrays[1] = BitConverter.GetBytes(-m_Bodys[m_Id].transform.forward.y);
                arrays[2] = BitConverter.GetBytes(-m_Bodys[m_Id].transform.forward.z);
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 4; j++)
                        m_ItemInfo[1 + i * 4 + j] = arrays[i][j];
                m_ItemInfo[m_ItemInfo.Length - 2] = 0;
                m_ItemInfo[m_ItemInfo.Length - 1] = (byte)m_Id;

                m_SendItem = true;
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
                m_PosAndRot[m_PosAndRot.Length - 1] = (byte)m_Id;
                m_SendPosition = true;
            }

            TimeSpan time = DateTime.Now - m_StartTime;
            int rank = 0;
            float z = m_Bodys[m_Id].transform.position.z;
            for (int i = 0; i < PLAYER_NUM; i++)
                if (z <= m_Bodys[i].transform.position.z)
                    rank++;

            m_CarUI.setTimeText(string.Format("{0:D2}:{1:D2}:{2:D3}", time.Minutes, time.Seconds, time.Milliseconds));
            m_CarUI.setRank(rank + "/" + PLAYER_NUM);
            m_CarUI.setSpeed(m_Controllers[m_Id].CurrentSpeed);
            m_CarUI.setUTurn(m_Bodys[m_Id].transform.forward.z < 0);
            m_CarUI.setItem(m_Item);
        }

        if (m_Finish)
        {
            m_Finish = false;
            m_CarUI.setInfoText("The Game will be restarted automatically after " + (5 + m_Id * 3) + " seconds.");
            new Thread(GameFinishListener).Start();
        }

        if (m_Destroy)
        {
            m_Destroy = false;
            for (int i = 0; i < PLAYER_NUM; i++)
                Destroy(m_Bodys[i].gameObject);
            Destroy(m_Road);

            Initialize();
        }
    }

    private void FixedUpdate()
    {
        if (m_Start)
        {
            if (m_StartBoost)
            {
                for (int i = 0; i < PLAYER_NUM; i++)
                {
                    m_StartBoost = false;
                    if (m_Bodys[i].velocity.magnitude > 0)
                    {
                        m_Bodys[i].AddForce(m_Bodys[i].transform.forward * START_BOOST_FACTOR * m_PlayerStartBoosts[i], ForceMode.Impulse);
                        m_PlayerStartBoosts[i] = 0;
                    }
                    else
                        m_StartBoost = true;
                }
            }

            if (m_PlayerItemUpdate)
            {
                Vector3 forward = new Vector3(BitConverter.ToSingle(m_PlayerItemInfo, 0), BitConverter.ToSingle(m_PlayerItemInfo, 4), BitConverter.ToSingle(m_PlayerItemInfo, 8));
                if (m_PlayerItemInfo[m_PlayerItemInfo.Length - 2] == 0)
                    m_Bodys[m_PlayerItemInfo[m_PlayerItemInfo.Length - 1]].AddForce(forward * BOOST_FACTOR, ForceMode.Impulse);
                else
                {
                    Rigidbody bullet = Instantiate(Bullet, m_Bodys[m_PlayerItemInfo[m_PlayerItemInfo.Length - 1]].transform.position + forward * 10,
                        m_Bodys[m_PlayerItemInfo[m_PlayerItemInfo.Length - 1]].transform.rotation);
                    bullet.AddForce(forward * BULLET_FACTOR, ForceMode.Impulse);
                }
                m_PlayerItemUpdate = false;
            }

            for (int i = 0; i < PLAYER_NUM; i++)
                m_Controllers[i].Move((m_PlayerReversed[i] ? -1 : 1) * m_PlayerHorizontals[i], m_PlayerVerticals[i], m_PlayerVerticals[i], m_PlayerHandBrakes[i]);
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

    private void GameFinishListener()
    {
        m_PositionThread.Join();
        m_Thread.Join();

        Thread.Sleep(1000 * (5 + m_Id * 3));
        m_Start = false;
        m_Destroy = true;
    }


    private void ThreadListener()
    {
        m_BadItemSet = new HashSet<GameObject>();
        TcpClient client = new TcpClient();
        client.NoDelay = true;
        client.Client.NoDelay = true;
        NetworkStream ns = null;
        int port = 3000;

        try
        {
            client.Connect(new IPEndPoint(IPAddress.Parse("52.78.108.211"), port));
            ns = client.GetStream();

            byte[] data = new byte[1024];

            data[0] = 1;
            ns.Write(data, 0, data.Length);
            ns.Flush();

            ns.Read(data, 0, 1);
            m_Id = data[0];

            m_Change = true;
            m_Run = true;

            while (m_Run)
            {
                ns.Read(data, 0, 1);
                Debug.Log(data[0]);
                switch (data[0])
                {
                    case 0: // data from pad
                        ns.Read(data, 0, 2);
                        int id = data[1] >> 4;
                        if (id < PLAYER_NUM)
                        {
                            m_PlayerHorizontals[id] = 1 - ((float)data[0]) / 128;

                            m_PlayerHandBrakes[id] = (data[1] & 3) == 3 ? 1 : 0;

                            if ((data[1] & 1) == 1)
                                m_PlayerVerticals[id] = 1;
                            else if ((data[1] & 2) == 2)
                                m_PlayerVerticals[id] = -1;
                            else
                                m_PlayerVerticals[id] = 0;

                            if (id == m_Id && (m_Item == 0 || m_Item == 1) && (data[1] & 4) == 4)
                                m_UseItem = true;
                        }
                        break;
                    case 1: // data from player (position, rotation)
                        lock (m_Lock)
                        {
                            ns.Read(m_EnemyPosAndRot, 0, m_EnemyPosAndRot.Length);
                            if (m_EnemyPosAndRot[m_EnemyPosAndRot.Length - 1] != m_Id)
                                m_EnemyPositionUpdate = true;
                            else
                                m_EnemyPositionUpdate = false;
                        }
                        break;
                    case 2: // data from player (item)
                        ns.Read(m_PlayerItemInfo, 0, m_PlayerItemInfo.Length);
                        m_PlayerItemUpdate = true;
                        break;
                    case 3: // data from player (reversed)
                        ns.Read(data, 0, data.Length - 1);
                        m_PlayerReversed[data[0]] = true;
                        int id_ = data[0];
                        new Thread(() =>
                        {
                            Thread.Sleep(REVERSE_TIME);
                            m_PlayerReversed[id_] = false;
                        }).Start();
                        break;
                    case 250: // lose
                        m_CarUI.setText("Lose");
                        ns.Read(data, 0, data.Length - 1);
                        ns.Close();
                        client.Close();
                        m_Finish = true;
                        m_Run = false;
                        break;
                    case 251: // 1 sec after game start
                        m_CarUI.setText("");
                        break;
                    case 252: // game start
                        ns.Read(m_PlayerStartBoosts, 0, PLAYER_NUM);
                        m_CarUI.setText("Go!");
                        m_CarUI.setInfoText("");
                        m_StartTime = DateTime.Now;
                        m_StartBoost = true;
                        m_Start = true;
                        m_PositionThread = new Thread(SendPositionListener);
                        m_PositionThread.Start();
                        break;
                    case 253: // 1 sec before game start
                        m_CarUI.setText("1");
                        break;
                    case 254: // 2 sec before game start
                        m_CarUI.setText("2");
                        break;
                    case 255: // 3 sec before game start
                        m_CarUI.setText("3");
                        break;
                }

                if (m_SendPosition)
                {
                    m_SendPosition = false;
                    ns.Write(m_PosAndRot, 0, m_PosAndRot.Length);
                    ns.Flush();
                }

                if (m_SendItem)
                {
                    m_SendItem = false;
                    ns.Write(m_ItemInfo, 0, m_ItemInfo.Length);
                    ns.Flush();
                }

                if (m_ReverseSend)
                {
                    m_ReverseSend = false;
                    data[0] = 3;
                    data[1] = (byte)m_Id;
                    ns.Write(data, 0, data.Length);
                    ns.Flush();
                }

                if (m_SendFinish)
                {
                    m_SendFinish = false;
                    data[0] = 250;
                    ns.Write(data, 0, data.Length);
                    ns.Flush();
                    ns.Close();
                    client.Close();
                    m_Finish = true;
                    m_Run = false;
                    break;
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
            m_Run = false;
            if (ns != null)
                ns.Close();
            client.Close();
        }
    }
}