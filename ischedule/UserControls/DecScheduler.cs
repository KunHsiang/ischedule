﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ischedule.Properties;
using Sunset.Data;

namespace ischedule
{
    /// <summary>
    /// Decorator for Scheduler, it is a wrapper class for panel that contains a scheduler.
    /// </summary>
    public class DecScheduler
    {
        private DevComponents.DotNetBar.PanelEx pnlContainer;
        private SchedulerType schType = SchedulerType.Teacher;
        private int colCount = -1;
        private int rowCount = -1;

        private int colHeaderHeight = 30;
        private int rowHeaderWidth = 30;

        private Dictionary<string, DevComponents.DotNetBar.PanelEx> cells;  //所有的 panel
        private Dictionary<string, DecPeriod> decPeriods;   //所有的panel 的decorator
        private Dictionary<string, DevComponents.DotNetBar.PanelEx> headerCells;

        private List<CEvent> dataSource;        //所有的課程分段
        private TimeTable currentTbl;           //目前的上課時間表
        private List<Period> busyPeriods;       //不排課時段
        private List<Period> conflictPeriods;   //資源衝突，無法排課的節次清單
        private List<Period> readyPeriods;      //可以排課的節次清單

        private const int lvWho = 1;
        private const int lvWhom = 2;
        private const int lvWhere = 3;

        //local variables for property
        private int mType;
        private string mObjID;
        private string mEventID;
        private string mObjName = string.Empty;

        //local variables
        private Scheduler schLocal = Scheduler.Instance;
        private LPViewOption mOption = new LPViewOption();
        private frmProgress frmASProgress;
        private Dictionary<string, int> colTestEvents = new Dictionary<string, int>(); //Holds a list of test event ids
        private string idTestEvent = string.Empty; //Active test event id
        private List<string> idTestEvents = new List<string>();
        private string idTestCourseID = string.Empty;
        private string idXchgEvent = string.Empty; //Active exchange event id
        private Appointments apsCur;
        private TimeTable ttCur;
        private CEventBindingList Transfers = new CEventBindingList();
        private List<int> Periods = new List<int>();
        private List<DecPeriod> SelectedPeriods = new List<DecPeriod>(); //記錄目前選取的分課

        /// <summary>
        /// pnl : 整個課表的 container
        /// schType : 課表類型
        /// 
        /// </summary>
        /// <param name="pnl"></param>
        /// <param name="schType"></param>
        public DecScheduler(DevComponents.DotNetBar.PanelEx pnl)
        {
            this.pnlContainer = pnl;
            this.pnlContainer.Resize += (sender, e) => Resize();
            this.cells = new Dictionary<string, DevComponents.DotNetBar.PanelEx>();
            this.decPeriods = new Dictionary<string, DecPeriod>();
            this.headerCells = new Dictionary<string, DevComponents.DotNetBar.PanelEx>();

            #region Scheduler相關事件
            schLocal.AutoScheduleComplete += (sender, e) =>
            {
                string idBottom = e.EventList[e.BottomIndex].EventID;
                bool bUpdateContent = false;

                foreach (CEvent evtMember in e.EventList)
                {
                    if (IsRelatedEvent(evtMember.EventID))
                    {
                        if (evtMember.WeekDay != 0)
                        {
                            RemoveTestEvent(evtMember.EventID);
                        }

                        bUpdateContent = true;
                    }
                    if (evtMember.EventID == idBottom) return;
                }
                if (bUpdateContent) UpdateContent();
            };

            //自動排課中顯示進度
            schLocal.AutoScheduleProgress += (sender, e) =>
            {
                if (frmASProgress != null)
                {
                    frmASProgress.ChangeProgress(e.nCurIndex);
                    e.Cancel = frmASProgress.UserAbort;
                }            };

            schLocal.EventBeforeDelete += (sender, e) =>
            {
                if (idXchgEvent == e.EventID)
                    idXchgEvent = string.Empty;
                RemoveTestEvent(e.EventID);
            };

            schLocal.EventLocked += (sender, e) =>
            {
                if (IsRelatedEvent(e.EventID))
                    UpdateContent();
            };

            schLocal.EventPropertyChanged += (sender, e) =>
            {
                RemoveTestEvent(e.EventID);

                if (IsRelatedEvent(e.EventID))
                    AddTestEvent(e.EventID);
            };

            schLocal.EventScheduled += (sender, e) =>
            {
                RemoveTestEvent(e.EventID);
                if (IsRelatedEvent(e.EventID))
                    UpdateContent();
            };

            schLocal.EventsFreed += (sender, e) =>
            {
                foreach (CEvent evt in e.EventList)
                    if (IsRelatedEvent(evt.EventID))
                    {
                        idTestEvent = string.Empty;
                        idTestCourseID = string.Empty;
                        UpdateContent();
                        break;
                    }
            };

            schLocal.EventUnlocked += (sender, e) =>
            {
                if (IsRelatedEvent(e.EventID))
                    UpdateContent();
            };
            #endregion
        }

        [DllImport("kernel32.dll")]
        public static extern bool Beep(int BeepFreq, int BeepDuration);

        /// <summary>
        /// 當某一節次被按下時
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void dec_OnPeriodClicked(object sender, PeriodEventArgs e)
        {
            if (sender is DecPeriod)
            {
                DecPeriod Period = (DecPeriod)sender;
                List<string> SelectEventIDs = Period.Data.Select(x=>x.EventID).ToList();

                //Clear selected for all cells ;
                foreach (string key in this.decPeriods.Keys)
                {
                    List<string> EventIDs = this.decPeriods[key].Data.Select(x => x.EventID).ToList();

                    if (SelectEventIDs.Intersect(EventIDs).Count() > 0)
                        this.decPeriods[key].IsSelected = true;
                    else
                        this.decPeriods[key].IsSelected = false;
                }

                int WeekDay = e.Weekday;
                int PeriodNo = e.Period;

                //若是星期或節次小於或等於0則不繼續執行
                if (WeekDay <= 0 || PeriodNo <= 0) return;

                #region 若為可排課顏色則進行排課
                if (Period.BackColor.Equals(SchedulerColor.lvSchedulableBackColor))
                {
                    if (idTestEvents.Count > 0)
                    {
                        Cursor.Current = Cursors.WaitCursor;

                        List<string> idAddEvents = new List<string>();
                        List<string> idEvents = new List<string>();

                        idTestEvents.ForEach(x => idEvents.Add(x));
                        idTestEvents.Clear();

                        foreach (string idEvent in idEvents)
                        {
                            if (IsRelatedEvent(idEvent))
                            {
                                if (IsScheduled(idEvent))
                                    schLocal.FreeEvent(idEvent);

                                string nTimeTableID = GetTimeTableID(idEvent);

                                if (nTimeTableID == ttCur.TimeTableID)
                                {
                                    //若拖到畫面上的nPeriodNo或nWeekDay為0則加入測試事件
                                    if (PeriodNo == 0 || WeekDay == 0)
                                    {
                                        AddTestEvent(idEvent);
                                    }
                                    else
                                    {
                                        //實際安排事件
                                        bool localScheduled = schLocal.ScheduleEvent(idEvent, WeekDay, PeriodNo);

                                        //若無法安排事件，則發出Beep聲，並加入測試事件
                                        if (!localScheduled)
                                        {
                                            Beep(1, 1);
                                            idAddEvents.Add(idEvent);
                                        }
                                    }
                                }
                                else
                                    idAddEvents.Add(idEvent);
                            }
                        }

                        idTestEvent = string.Empty;

                        idAddEvents.ForEach(x => AddTestEvent(x));

                        Cursor.Current = Cursors.Arrow;
                    }
                    //嘗試解析事件編號
                    else if (!string.IsNullOrEmpty(idTestEvent))
                    {
                        string idEvent = idTestEvent;

                        //判斷是否為相關事件或相同的時間表才繼續執行
                        if (IsRelatedEvent(idEvent))
                        {
                            if (IsScheduled(idEvent))
                                schLocal.FreeEvent(idEvent);

                            string nTimeTableID = GetTimeTableID(idEvent);

                            if (nTimeTableID == ttCur.TimeTableID)
                            {
                                Cursor.Current = Cursors.WaitCursor;

                                //若拖到畫面上的nPeriodNo或nWeekDay為0則加入測試事件
                                if (PeriodNo == 0 || WeekDay == 0)
                                {
                                    AddTestEvent(idEvent);
                                }
                                else
                                {
                                    idTestEvent = string.Empty;
                                    //實際安排事件
                                    bool localScheduled = schLocal.ScheduleEvent(idEvent, WeekDay, PeriodNo);

                                    //若無法安排事件，則發出Beep聲，並加入測試事件
                                    if (!localScheduled)
                                    {
                                        Beep(1, 1);
                                        AddTestEvent(idEvent);
                                    }
                                }

                                Cursor.Current = Cursors.Arrow;
                            }
                            else
                                AddTestEvent(idEvent);
                        }
                    }
                }
                #endregion
                #region 已排課狀況
                else if (Period.BackColor.Equals(SchedulerColor.lvScheduledBackColor))
                {
                    Cursor.Current = Cursors.WaitCursor;

                    string idEvent = GetEventID(WeekDay, PeriodNo);

                    if (!string.IsNullOrEmpty(idEvent))
                    {
                        CEvent evtTest = schLocal.CEvents[idEvent];

                        if (!string.IsNullOrEmpty(idEvent) && !evtTest.ManualLock)
                        {
                            SetTestEvent(idEvent);
                            UpdateContent();
                        };
                    }

                    Cursor.Current = Cursors.Arrow;
                }
                #endregion
            }
            else if (sender is PictureBox)
            {
                PictureBox vPictureBox = sender as PictureBox;

                string strTag = "" + vPictureBox.Tag;

                int WeekDay = e.Weekday;
                int PeriodNo = e.Period;

                //若是星期或節次小於或等於0則不繼續執行
                if (WeekDay <= 0 || PeriodNo <= 0) return;

                if (strTag.Equals("delete"))
                {
                    Cursor.Current = Cursors.WaitCursor;

                    string idEvent = Unschedule(WeekDay, PeriodNo);

                    Cursor.Current = Cursors.Arrow;
                }
                else if (strTag.Equals("busy"))
                {
 
                }
                else
                {
                    Cursor.Current = Cursors.WaitCursor;

                    ToggleLock(WeekDay, PeriodNo);

                    Cursor.Current = Cursors.Arrow;
                }
            }
            else if (sender is Label)
            {
                Label vLabel = sender as Label;

                string strTag = "" + vLabel.Tag;

                if (!string.IsNullOrEmpty(strTag))
                {
                    string[] strEvents = strTag.Split(new char[] { '：' });

                    if (strEvents.Length == 2)
                    {

                        string AssocType = strEvents[0];
                        string AssocID = strEvents[1];
                        string SelectedTab = MainFormBL.Instance.GetSelectedType();

                        if (!string.IsNullOrEmpty(AssocType) && !string.IsNullOrEmpty(AssocID))
                        {
                            if (AssocType.Equals("Teacher"))
                            {
                                switch (SelectedTab)
                                {
                                    case "Teacher":
                                        MainFormBL.Instance.OpenTeacherLPView(lvWho, AssocID, true, string.Empty, false);
                                        break;
                                    case "Class":
                                        MainFormBL.Instance.OpenClassLPView(lvWho, AssocID, true, string.Empty, false);
                                        break;
                                    case "Classroom":
                                        MainFormBL.Instance.OpenClassroomLPView(lvWho, AssocID, true, string.Empty, false);
                                        break;
                                }
                            }
                            else if (AssocType.Equals("Class"))
                            {
                                switch (SelectedTab)
                                {
                                    case "Teacher":
                                        MainFormBL.Instance.OpenTeacherLPView(lvWhom, AssocID, true, string.Empty, false);
                                        break;
                                    case "Class":
                                        MainFormBL.Instance.OpenClassLPView(lvWhom, AssocID, true, string.Empty, false);
                                        break;
                                    case "Classroom":
                                        MainFormBL.Instance.OpenClassroomLPView(lvWhom, AssocID, true, string.Empty, false);
                                        break;
                                }
                            }
                            else if (AssocType.Equals("Classroom"))
                            {
                                switch (SelectedTab)
                                {
                                    case "Teacher":
                                        MainFormBL.Instance.OpenTeacherLPView(lvWhere, AssocID, true, string.Empty, false);
                                        break;
                                    case "Class":
                                        MainFormBL.Instance.OpenClassLPView(lvWhere, AssocID, true, string.Empty, false);
                                        break;
                                    case "Classroom":
                                        MainFormBL.Instance.OpenClassroomLPView(lvWhere, AssocID, true, string.Empty, false);
                                        break;
                                }
                            }
                        }
                    }
                }
            }

            #region 暫不實作
            //    try
            //    {
            //        if (e.RightButton == MouseButtonState.Pressed)
            //            return;

            //        int WeekDay = Constants.NullValue;
            //        int PeriodNo = Constants.NullValue;

            //        Point MousePoint = DataGridHelper.GetMouseRowColumn(e.OriginalSource);

            //        if (((int)MousePoint.X != Constants.NullValue) && ((int)MousePoint.Y != Constants.NullValue))
            //        {
            //            dynamic SelectedItem = fgLPView.Items[(int)MousePoint.Y];

            //            WeekDay = (int)MousePoint.X;
            //            PeriodNo = SelectedItem.PeriodNo;
            //        }

            //        if ((e.LeftButton == MouseButtonState.Pressed && (Keyboard.IsKeyDown(Key.LeftAlt)))
            //            || (e.LeftButton == MouseButtonState.Pressed && Keyboard.IsKeyToggled(Key.RightAlt)))
            //        {
            //            if ((WeekDay != Constants.NullValue) && (PeriodNo != Constants.NullValue))
            //                ToggleLock(WeekDay, PeriodNo);
            //        }
            //        else if (e.LeftButton == MouseButtonState.Pressed && IsDeleteImage(e.OriginalSource))
            //        {
            //            Image CurrentImage = (e.OriginalSource as System.Windows.Controls.Image);

            //            string strImage = "" + CurrentImage.Source;

            //            if (strImage.Contains(strDeleteEventPath))
            //            {
            //                if (WeekDay <= 0 || PeriodNo <= 0) return;

            //                Cursor = Cursors.Wait;

            //                string idEvent = Unschedule(WeekDay, PeriodNo);

            //                if (!string.IsNullOrEmpty(idEvent))
            //                {
            //                    AddTestEvent(idEvent);
            //                    UpdateTestEventSolutionCount();
            //                }

            //                CEvent localEvent = schLocal.CEvents[idEvent];

            //                Cursor = Cursors.Arrow;

            //            }
            //        }
            //        else if (e.LeftButton == MouseButtonState.Pressed)
            //        {
            //            //若是星期或節次小於或等於0則不繼續執行
            //            if (WeekDay <= 0 || PeriodNo <= 0) return;

            //            string CellBackColor = GetCellBackColor((DependencyObject)e.OriginalSource);

            //            if (CellBackColor.Equals(lvSchedulableBackColor))
            //            {
            //                if (idTestEvents.Count > 0)
            //                {
            //                    Cursor = Cursors.Wait;

            //                    List<string> idAddEvents = new List<string>();
            //                    List<string> idEvents = new List<string>();

            //                    idTestEvents.ForEach(x => idEvents.Add(x));
            //                    idTestEvents.Clear();

            //                    foreach (string idEvent in idEvents)
            //                    {
            //                        if (IsRelatedEvent(idEvent))
            //                        {
            //                            if (IsScheduled(idEvent))
            //                                schLocal.FreeEvent(idEvent);

            //                            string nTimeTableID = GetTimeTableID(idEvent);

            //                            if (nTimeTableID == ttCur.TimeTableID)
            //                            {
            //                                //若拖到畫面上的nPeriodNo或nWeekDay為0則加入測試事件
            //                                if (PeriodNo == 0 || WeekDay == 0)
            //                                {
            //                                    AddTestEvent(idEvent);
            //                                }
            //                                else
            //                                {
            //                                    //實際安排事件
            //                                    bool localScheduled = schLocal.ScheduleEvent(idEvent, WeekDay, PeriodNo);

            //                                    //若無法安排事件，則發出Beep聲，並加入測試事件
            //                                    if (!localScheduled)
            //                                    {
            //                                        Beep(1, 1);
            //                                        idAddEvents.Add(idEvent);
            //                                    }
            //                                }
            //                            }
            //                            else
            //                                idAddEvents.Add(idEvent);
            //                        }
            //                    }

            //                    idTestEvent = string.Empty;

            //                    idAddEvents.ForEach(x => AddTestEvent(x));

            //                    UpdateTestEventSolutionCount();

            //                    Cursor = Cursors.Arrow;
            //                }
            //                //嘗試解析事件編號
            //                else if (!string.IsNullOrEmpty(idTestEvent))
            //                {
            //                    string idEvent = idTestEvent;

            //                    //判斷是否為相關事件或相同的時間表才繼續執行
            //                    if (IsRelatedEvent(idEvent))
            //                    {
            //                        if (IsScheduled(idEvent))
            //                            schLocal.FreeEvent(idEvent);

            //                        string nTimeTableID = GetTimeTableID(idEvent);

            //                        if (nTimeTableID == ttCur.TimeTableID)
            //                        {
            //                            Cursor = Cursors.Wait;

            //                            //若拖到畫面上的nPeriodNo或nWeekDay為0則加入測試事件
            //                            if (PeriodNo == 0 || WeekDay == 0)
            //                            {
            //                                AddTestEvent(idEvent);
            //                                UpdateTestEventSolutionCount();
            //                            }
            //                            else
            //                            {
            //                                idTestEvent = string.Empty;
            //                                //實際安排事件
            //                                bool localScheduled = schLocal.ScheduleEvent(idEvent, WeekDay, PeriodNo);

            //                                //若無法安排事件，則發出Beep聲，並加入測試事件
            //                                if (!localScheduled)
            //                                {
            //                                    Beep(1, 1);
            //                                    AddTestEvent(idEvent);
            //                                    UpdateTestEventSolutionCount();
            //                                }
            //                            }

            //                            Cursor = Cursors.Arrow;
            //                        }
            //                        else
            //                            AddTestEvent(idEvent);
            //                    }
            //                }
            //            }
            //            else if (CellBackColor.Equals(lvScheduledBackColor))
            //            {
            //                Cursor = Cursors.Wait;

            //                grdTestEvent.SelectedIndex = -1;

            //                string idEvent = GetEventID(WeekDay, PeriodNo);

            //                if (!string.IsNullOrEmpty(idEvent))
            //                {
            //                    CEvent evtTest = schLocal.CEvents[idEvent];

            //                    if (!string.IsNullOrEmpty(idEvent) && !evtTest.ManualLock)
            //                    {
            //                        SetTestEvent(idEvent);
            //                        UpdateContent();
            //                    };
            //                }

            //                Cursor = Cursors.Arrow;
            //            }
            //        }
            //    }
            //    catch (Exception ve)
            //    {
            //        FISCA.ErrorBox.Show("排入分課時發生錯誤！" + System.Environment.NewLine + ve.Message, ve);
            //    }
            #endregion
        }

        #region ==== Methods ======

        public List<CEvent> DataSource
        {
            get { return this.dataSource; }
            set { 
                this.dataSource = value;
                this.fillData();
            }
        }

        public void SetCourseSections(List<CEvent> data)
        {
            this.dataSource = data;
        }        

        public void SetTimeTable(TimeTable tbl)
        {
            if (this.currentTbl != null && this.currentTbl.TimeTableID == tbl.TimeTableID)
                return;
            else
            {
                this.currentTbl = tbl;
                this.colCount = tbl.Periods.MaxWeekDay;
                this.rowCount = tbl.Periods.MaxPeriodNo;
                //this.createHeaders();
                this.createCells();
            }
        }

        public void InitSchedule(int weekday, int periodcount)
        {
            this.currentTbl = null;
            this.colCount = weekday;
            this.rowCount = periodcount;
            //this.createHeaders();
            this.createCells();
        }

        public void SetBusyPeriods(List<Period> busyPeriods)
        {
            this.busyPeriods = busyPeriods;
            foreach (Period pd in this.busyPeriods)
            {
                string key = string.Format("{0}_{1}", pd.WeekDay, pd.PeriodNo);
                DecPeriod decPd = this.decPeriods[key];
                decPd.SetAsBusy("不排課時段");
            }
        }

        /* 重新調整大小 */
        public void Resize()
        {
            this.RedrawGrid();
        }

        /*  填入資料   */
        public void fillData()
        {
            if (this.dataSource == null)
                return;

            Dictionary<string, CEvent> dicTemp = new Dictionary<string, CEvent>();
            foreach (CEvent v in this.dataSource)
            {
                if (v.WeekDay > 0)
                {
                    string key = string.Format("{0}_{1}", v.WeekDay, v.PeriodNo);
                    dicTemp.Add(key, v);
                }
            }

            foreach (string key in this.decPeriods.Keys)
            {
                //if (dicTemp.ContainsKey(key))
                //    this.decPeriods[key].Data = dicTemp[key];
                //else
                //    this.decPeriods[key].Data = null;
            }
        }

        #endregion

        #region ======= private functions =======
       

        private void createCells()
        {
            if (this.pnlContainer == null)
                return;

            //prepare timetable periods，便於判斷是否是無效的格子。
            Dictionary<string, Period> dicTimeTablePeriods = new Dictionary<string,Period>();    //上課時間表的所有節次，便於搜尋
            if (this.currentTbl != null) 
            {
                foreach (Period pd in this.currentTbl.Periods)
                {
                    string key = string.Format("{0}_{1}", pd.WeekDay, pd.PeriodNo);
                    dicTimeTablePeriods.Add(key, pd);
                }
            }
            bool isInit = (dicTimeTablePeriods.Count == 0);     //通常是在畫面初始化時期才為 true

            //hide all Cells
            foreach (string key in this.cells.Keys)
                this.cells[key].Visible = false;

            this.pnlContainer.SuspendLayout();
            this.pnlContainer.Controls.Clear();

            int pnlWidth = (this.pnlContainer.Size.Width - this.rowHeaderWidth) / colCount;
            int pnlHeight = (this.pnlContainer.Size.Height - this.colHeaderHeight) / rowCount;

            /* Create Headers */
            for (int i = 0; i < colCount; i++)
            {
                Point p = new Point(rowHeaderWidth + i * pnlWidth, 0);
                Size s = new Size(pnlWidth, colHeaderHeight);
                string name = string.Format("header_0_{0}", (i + 1).ToString());
                DevComponents.DotNetBar.PanelEx pnl = null;
                if (this.headerCells.ContainsKey(name))
                {
                    pnl = this.headerCells[name];
                    pnl.Location = p;
                    pnl.Size = s;
                    pnl.Visible = true;
                }
                else
                {
                    pnl = this.makePanel(name, (i+1).ToString(), p, s);
                    this.headerCells.Add(name, pnl);
                }
                this.pnlContainer.Controls.Add(pnl);
            }
            for (int j = 0; j < rowCount; j++)
            {
                Point p = new Point(0, colHeaderHeight + j * pnlHeight);
                Size s = new Size(rowHeaderWidth, pnlHeight);
                string name = string.Format("header_{0}_0", (j + 1).ToString());
                DevComponents.DotNetBar.PanelEx pnl = null;
                if (this.headerCells.ContainsKey(name))
                {
                    pnl = this.headerCells[name];
                    pnl.Location = p;
                    pnl.Size = s;
                    pnl.Visible = true;
                }
                else
                {
                    pnl = this.makePanel(name, (j+1).ToString(), p, s);
                    this.headerCells.Add(name, pnl);
                }
                this.pnlContainer.Controls.Add(pnl);
            }

            /* Originize Cells */
            for (int i = 0; i < colCount; i++)
            {
                for (int j = 0; j < rowCount; j++)
                {
                    string name = string.Format("{0}_{1}", (i+1).ToString(), (j+1).ToString());
                    Point p = new Point(rowHeaderWidth + i * pnlWidth, colHeaderHeight + j * pnlHeight);
                    Size s = new Size(pnlWidth, pnlHeight);

                    DevComponents.DotNetBar.PanelEx pnl = null;
                    if (this.cells.ContainsKey(name))
                    {
                        pnl = this.cells[name];
                        pnl.Location = p;
                        pnl.Size = s;
                        pnl.Visible = true;
                    }
                    else
                    {
                        pnl = this.makePanel(name, "", p, s);
                        this.cells.Add(name, pnl);
                        DecPeriod dec = new DecPeriod(pnl, i+1, j+1, SchedulerType.Teacher);
                        dec.BackColor = Color.White;
                        this.decPeriods.Add(name, dec);
                        dec.OnPeriodClicked += new PeriodClickedHandler(dec_OnPeriodClicked);
                    }

                    DecPeriod decP = this.decPeriods[name];
                    decP.IsValid = (isInit) ? true :  dicTimeTablePeriods.ContainsKey(name);
                    
                    this.pnlContainer.Controls.Add(pnl);
                }
            }
            this.pnlContainer.ResumeLayout();
        }

        private DevComponents.DotNetBar.PanelEx makePanel(string name, string txt, Point location, Size size)
        {
            DevComponents.DotNetBar.PanelEx pnl = new DevComponents.DotNetBar.PanelEx();
            pnl.CanvasColor = System.Drawing.SystemColors.Control;
            pnl.ColorSchemeStyle = DevComponents.DotNetBar.eDotNetBarStyle.StyleManagerControlled;
            pnl.Location = location;
            pnl.Name = name;
            pnl.Size = size;
            pnl.Style.Alignment = System.Drawing.StringAlignment.Center;
            pnl.Style.BackColor1.ColorSchemePart = DevComponents.DotNetBar.eColorSchemePart.PanelBackground;
            pnl.Style.BackColor2.ColorSchemePart = DevComponents.DotNetBar.eColorSchemePart.PanelBackground2;
            pnl.Style.Border = DevComponents.DotNetBar.eBorderType.SingleLine;
            pnl.Style.BorderColor.ColorSchemePart = DevComponents.DotNetBar.eColorSchemePart.PanelBorder;
            pnl.Style.ForeColor.ColorSchemePart = DevComponents.DotNetBar.eColorSchemePart.PanelText;
            pnl.Style.GradientAngle = 90;
            pnl.TabIndex = 1;
            pnl.Text = txt;

            return pnl;
        }

        #endregion

        #region LPView public method
        public void SetType(int AssocType, string EventID,bool IsSelect)
        {
            mType = AssocType;
            
        }

        public void SetAssocObject(int AssocType, string AssocID)
        {
            #region 初始化區域變數及功課表抬頭
            mType = AssocType;
            mObjID = AssocID;
            idTestEvent = string.Empty; //測試的EventID
            idXchgEvent = string.Empty; //調課的EventID

            switch (AssocType)
            {
                case lvWho:
                    schLocal.Teachers[mObjID].UseAppointments(0);    //預設使用第一個行事曆
                    apsCur = schLocal.Teachers[mObjID].Appointments; //取得資源已被排定的約會
                    ttCur = schLocal.TimeTables[0];
                    //Tag = schLocal.Whos[mObjID].Name + "功課表";
                    mObjName = schLocal.Teachers[mObjID].Name;
                    break;
                case lvWhom:
                    apsCur = schLocal.Classes[mObjID].Appointments;
                    ttCur = schLocal.TimeTables[schLocal.Classes[mObjID].TimeTableID];
                    //Tag = schLocal.Whoms[mObjID].Name + "功課表";
                    mObjName = schLocal.Classes[mObjID].Name;
                    //cmdXchgEvent.Visibility = System.Windows.Visibility.Visible;
                    break;
                case lvWhere:
                    apsCur = new Appointments();

                    for (int i = 0; i < schLocal.Classrooms[mObjID].Capacity; i++)
                    {
                        schLocal.Classrooms[mObjID].UseAppointments(i); //預設使用第一個行事曆

                        foreach (Appointment App in schLocal.Classrooms[mObjID].Appointments)
                            apsCur.Add(App);
                    }
                    ttCur = schLocal.TimeTables[0];
                    //Tag = schLocal.Wheres[mObjID].Name + "功課表";
                    mObjName = schLocal.Classrooms[mObjID].Name;
                    break;
            }
            #endregion

            #region 初始化行事曆數量
            mOption.CapacityIndex = 0;
            #endregion

            //Textboxes and imgTest
            ClearTestEvent();

            #region 初始化顯示教師、班級、場地及科目選項
            //Checkboxes
            switch (AssocType)
            {
                case lvWho:
                    mOption.IsWho = false;
                    mOption.IsWhom = true;
                    mOption.IsWhere = true;
                    mOption.IsWhat = true;
                    mOption.IsWhatAlias = false;
                    break;
                case lvWhom:
                    mOption.IsWho = true;
                    mOption.IsWhom = false;
                    mOption.IsWhere = true;
                    mOption.IsWhat = true;
                    mOption.IsWhatAlias = false;
                    break;
                case lvWhere:
                    mOption.IsWho = true;
                    mOption.IsWhom = true;
                    mOption.IsWhere = false;
                    mOption.IsWhat = true;
                    mOption.IsWhatAlias = false;
                    break;
            }
            #endregion
        }

        /// <summary>
        /// 選取測試分課
        /// </summary>
        /// <param name="Item"></param>
        public void SelectTestEvent(string EventID)
        {
            if (!string.IsNullOrEmpty(EventID))
            {
                SetTestEvent(EventID);
                ChangeTimeTable(GetTimeTableID(EventID));
            }
            else
            {
                idTestEvent = string.Empty;
                RedrawGrid();
                UpdateContent();
            }
        }

        /// <summary>
        /// 根據分課表編號同步TimeTable編號
        /// </summary>
        /// <param name="EventID">分課表編號</param>
        public void SyncTimeTable(string EventID)
        {
            if (!string.IsNullOrEmpty(EventID))
                ChangeTimeTable(GetTimeTableID(EventID));
        }
        #endregion

        #region private function

        /// <summary>
        /// 取得SchedulerType
        /// </summary>
        /// <returns></returns>
        private SchedulerType GetSchedulerType(int AssocType)
        {
            SchedulerType vSchedulerType = SchedulerType.Teacher;

            switch (AssocObjType)
            {
                case lvWho:
                    vSchedulerType = SchedulerType.Teacher;
                    break;
                case lvWhom:
                    vSchedulerType = SchedulerType.Class;
                    break;
                case lvWhere:
                    vSchedulerType = SchedulerType.Place;
                    break;
            }

            return vSchedulerType;
        }

        /// <summary>
        /// 判斷某個事件是否已被排程
        /// </summary>
        /// <param name="EventID">事件編號</param>
        /// <returns></returns>
        private bool IsScheduled(string EventID)
        {
            return schLocal.CEvents[EventID].WeekDay != 0;
        }

        /// <summary>
        /// 根據事件編號判斷事件的資源屬性是否與ObjID相等
        /// </summary>
        /// <param name="EventID"></param>
        /// <returns></returns>
        private bool IsRelatedEvent(string EventID)
        {
            //根據事件編號取得事件
            CEvent evtTest = schLocal.CEvents[EventID];

            //Determine if this event is related to this eventlist
            //根據LPView的判斷：
            //型態是lvWho，則判斷事件的教師編號是否會與mObjID相等
            //型態是lvWhom，則判斷事件的班級編號是否會與mObjID相等
            //型態是lvWhere，則判斷事件的場地編號是否會與mObjID相等
            switch (mType)
            {
                case lvWho:
                    return evtTest.TeacherID1 == mObjID || evtTest.TeacherID2 == mObjID || evtTest.TeacherID3 == mObjID;
                case lvWhom:
                    return evtTest.ClassID == mObjID;
                case lvWhere:
                    return evtTest.ClassroomID == mObjID;
            }

            return false;
        }

        /// <summary>
        /// 更新時間表
        /// Change the current Timetable view and update the grids
        /// </summary>
        /// <param name="nTimeTableID"></param>
        private void ChangeTimeTable(string nTimeTableID)
        {
            mOption.TimeTableID = nTimeTableID;

            ttCur = schLocal.TimeTables[nTimeTableID];

            RedrawGrid();

            UpdateContent();
        }

        /// <summary>
        /// 根據事件編號取得時間表編號
        /// </summary>
        /// <param name="EventID">事件編號</param>
        /// <returns>時間表編號</returns>
        private string GetTimeTableID(string EventID)
        {
            return string.IsNullOrEmpty(EventID) ? string.Empty : schLocal.CEvents[EventID].TimeTableID;
        }

        /// <summary>
        /// 根據星期及節次釋放分課
        /// </summary>
        /// <param name="nWeekDay">星期幾</param>
        /// <param name="nPeriodNo">節次</param>
        /// <returns>事件編號</returns>
        private string Unschedule(int nWeekDay, int nPeriodNo)
        {
            Period prdTest;
            Appointments appTests;
            Appointment appTest;

            #region Current TimeTable的Period存在才繼續
            prdTest = ttCur.Periods.GetPeriod(nWeekDay, nPeriodNo);

            if (prdTest == null)
                return string.Empty;
            #endregion

            #region Current Appointment存在才繼續
            appTests = apsCur.GetAppointments(prdTest.WeekDay, prdTest.BeginTime, prdTest.Duration);

            if (appTests.Count == 0)
                return string.Empty;
            else if (appTests.Count == 1 && !string.IsNullOrEmpty(appTests[0].EventID))
                appTest = appTests[0];
            else if (appTests.Count >= 2)
            {
                //List<string> EventIDs = new List<string>();

                //foreach(Appointment app in appTests)
                //{
                //    if (!string.IsNullOrEmpty(app.EventID))
                //        EventIDs.Add(app.EventID);
                //}

                //if (EventIDs.Count > 0)
                //{
                //    frmEventSelector EventSelector = new frmEventSelector(EventIDs);

                //    if (EventSelector.ShowDialog()== true)
                //    {
                //        #region 沒有鎖定才繼續
                //        CEvent localEvent = schLocal.CEvents[appTest.EventID];

                //        if (localEvent.ManualLock) return string.Empty;
                //        #endregion

                //        #region 嘗試釋放事件
                //        if (GetTimeTableID(appTest.EventID) == ttCur.TimeTableID)
                //        {
                //            schLocal.FreeEvent(appTest.EventID);
                //            return appTest.EventID;
                //        }
                //        #endregion
                //    }
                //}

                appTest = appTests[0];

                foreach (Appointment vapp in appTests)
                {
                    if (!string.IsNullOrEmpty(vapp.EventID) && vapp.EventID.Equals(idTestEvent))
                        appTest = vapp;
                }
            }
            else
                return string.Empty;
            #endregion

            //取得Appointment的EventID
            if (string.IsNullOrEmpty(appTest.EventID)) return string.Empty;

            #region 沒有鎖定才繼續
            CEvent localEvent = schLocal.CEvents[appTest.EventID];

            if (localEvent.ManualLock) return string.Empty;
            #endregion

            #region 嘗試釋放事件
            if (GetTimeTableID(appTest.EventID) == ttCur.TimeTableID)
            {
                schLocal.FreeEvent(appTest.EventID);
                return appTest.EventID;
            }
            #endregion

            return string.Empty;
        }

        /// <summary>
        /// 根據星期及節次釋放分課
        /// </summary>
        /// <param name="nWeekDay">星期幾</param>
        /// <param name="nPeriodNo">節次</param>
        /// <returns>事件編號</returns>
        private List<string> Unschedule(List<string> EventIDs)
        {
            List<string> UnscheduleEventIDs = new List<string>();

            foreach (string EventID in EventIDs)
            {
                if (!string.IsNullOrEmpty(EventID))
                {
                    CEvent localEvent = schLocal.CEvents[EventID];

                    if (!localEvent.ManualLock)
                    {
                        if (GetTimeTableID(EventID) == ttCur.TimeTableID)
                        {
                            schLocal.FreeEvent(EventID);
                            UnscheduleEventIDs.Add(EventID);
                        }
                    }
                }
            }

            return UnscheduleEventIDs;
        }

        /// <summary>
        /// 根據星期及節次取得事件編號
        /// </summary>
        /// <param name="nWeekDay"></param>
        /// <param name="nPeriodNo"></param>
        /// <returns></returns>
        private string GetEventID(int nWeekDay, int nPeriodNo)
        {
            Appointments appTests;
            Appointment appTest = null;

            #region Period存在才繼續
            Period prdTest = ttCur.Periods.GetPeriod(nWeekDay, nPeriodNo);

            if (prdTest == null) return string.Empty;
            #endregion

            #region Appointment存在才繼續
            //Get appontment that occupied the period
            appTests = apsCur.GetAppointments(prdTest.WeekDay, prdTest.BeginTime, prdTest.Duration);

            //若是Appointment不為null才繼續
            /*
            if (appTests.Count == 0)
                return string.Empty;
            else if (appTests.Count == 1 && !string.IsNullOrEmpty(appTests[0].EventID))
                appTest = appTests[0];
            else if (appTests.Count == 2 && !string.IsNullOrEmpty(appTests[0].EventID) && !string.IsNullOrEmpty(appTests[1].EventID))
                appTest = appTests[1].WeekFlag == 2 ? appTests[1] : appTests[0];
            else if (appTests.Count > 2 && !string.IsNullOrEmpty(appTests[0].EventID))
                appTest = appTests[0];
            else
                return string.Empty;
             */

            if (appTests.Count == 0)
                return string.Empty;
            else if (appTests.Count == 1 && !string.IsNullOrEmpty(appTests[0].EventID))
                appTest = appTests[0];
            else if (appTests.Count >= 2)
            {
                List<string> EventIDs = new List<string>();

                foreach (Appointment appSelector in appTests)
                    if (!string.IsNullOrEmpty(appSelector.EventID))
                        EventIDs.Add(appSelector.EventID);

                frmEventSelector Selector = new frmEventSelector(EventIDs);

                if (Selector.ShowDialog() == DialogResult.OK)
                {
                    List<string> vEventIDs = Selector.SelectedEventIDs;

                    if (Selector.SelectorType == 1)
                    {
                        if (vEventIDs.Count == 1)
                        {
                            string vEventID = vEventIDs[0];
                            CEvent evtTest = schLocal.CEvents[vEventID];

                            if (!string.IsNullOrEmpty(vEventID) && !evtTest.ManualLock)
                            {
                                SetTestEvent(vEventID);
                                UpdateContent();
                            };

                            idTestEvents.Clear();
                        }
                        else
                        {
                            foreach (string vEventID in vEventIDs)
                            {
                                CEvent evtTest = schLocal.CEvents[vEventIDs[0]];

                                if (!evtTest.ManualLock)
                                    idTestEvents.Add(vEventID);
                            }

                            if (idTestEvents.Count > 0)
                            {
                                SetTestEvent(idTestEvents[0]);
                                idTestEvents = vEventIDs;

                                UpdateContent();
                            }
                        }
                    }
                    else if (Selector.SelectorType == 2)
                    {
                        List<string> idEvents = Unschedule(vEventIDs);

                        if (idEvents.Count > 0)
                        {
                            idEvents.ForEach(x => AddTestEvent(x));
                        }
                    }

                    return string.Empty;
                }
            }
            else
                return string.Empty;
            #endregion

            //若是EventID不為0才繼續
            //if (string.IsNullOrEmpty(appTest.EventID)) return string.Empty;

            //Make sure the event is allocated under same timetable
            if (appTest != null)
                if (GetTimeTableID(appTest.EventID) == ttCur.TimeTableID)
                    return appTest.EventID;

            return string.Empty;
        }

        /// <summary>
        /// Toggle the lock condition of a particular event indicated by Weekday and period
        /// </summary>
        /// <param name="nWeekDay">星期</param>
        /// <param name="nPeriodNo">節次</param>
        private void ToggleLock(int nWeekDay, int nPeriodNo)
        {
            Appointment appTest;

            #region Period存在才繼續
            Period prdTest = ttCur.Periods.GetPeriod(nWeekDay, nPeriodNo);

            if (prdTest == null) return;
            #endregion

            #region Appointment存在才繼續
            //Get appontment that occupied the period
            appTest = apsCur.GetAppointment(prdTest.WeekDay, prdTest.BeginTime, prdTest.Duration, mOption.WeekFlag);

            //若是Appointment不為null才繼續
            if (appTest == null) return;
            #endregion

            //若是EventID不為0才繼續
            if (string.IsNullOrEmpty(appTest.EventID)) return;

            //Make sure the event is allocated under same timetable
            if (GetTimeTableID(appTest.EventID) == ttCur.TimeTableID)
            {
                if (schLocal.CEvents[appTest.EventID].ManualLock)
                    schLocal.UnlockEvent(appTest.EventID);
                else
                    schLocal.LockEvent(appTest.EventID);
            }
        }

        //private DevComponents.DotNetBar.PanelEx CreatePanel(string name, string txt, Point location, Size size)
        //{
        //    DevComponents.DotNetBar.PanelEx pnl = new DevComponents.DotNetBar.PanelEx();
        //    pnl.CanvasColor = System.Drawing.SystemColors.Control;
        //    pnl.ColorSchemeStyle = DevComponents.DotNetBar.eDotNetBarStyle.StyleManagerControlled;
        //    pnl.Location = location;
        //    pnl.Name = name;
        //    pnl.Size = size;
        //    pnl.Style.Alignment = System.Drawing.StringAlignment.Center;
        //    pnl.Style.BackColor1.ColorSchemePart = DevComponents.DotNetBar.eColorSchemePart.PanelBackground;
        //    pnl.Style.BackColor2.ColorSchemePart = DevComponents.DotNetBar.eColorSchemePart.PanelBackground2;
        //    pnl.Style.Border = DevComponents.DotNetBar.eBorderType.SingleLine;
        //    pnl.Style.BorderColor.ColorSchemePart = DevComponents.DotNetBar.eColorSchemePart.PanelBorder;
        //    pnl.Style.ForeColor.ColorSchemePart = DevComponents.DotNetBar.eColorSchemePart.PanelText;
        //    pnl.Style.GradientAngle = 90;
        //    pnl.TabIndex = 1;
        //    pnl.Text = txt;

        //    return pnl;
        //}

        /// <summary>
        /// 根據TimeTable.Periods的MaxWeekDay來決定顯示的欄位
        /// </summary>
        private void RedrawGrid()
        {
            #region 取得時間表節次清單
            //特別注意節次列表，可能不會依序開始
            Periods.Clear();

            foreach (Period vPeriod in ttCur.Periods)
            {
                if (vPeriod.PeriodNo != 0)
                    if (!Periods.Contains(vPeriod.PeriodNo))
                        Periods.Add(vPeriod.PeriodNo);
            }

            Periods.Sort();

            colCount = ttCur.Periods.MaxWeekDay;
            rowCount = Periods.Count;
            #endregion

            if (this.pnlContainer == null)
                return;

            //hide all Cells
            foreach (string key in this.cells.Keys)
                this.cells[key].Visible = false;

            this.pnlContainer.SuspendLayout();
            this.pnlContainer.Controls.Clear();

            int pnlWidth = (this.pnlContainer.Size.Width - this.rowHeaderWidth) / colCount;
            int pnlHeight = (this.pnlContainer.Size.Height - this.colHeaderHeight) / rowCount;

            #region Create Headers
            //建立星期物件
            for (int i = 0; i < colCount; i++)
            {
                Point p = new Point(rowHeaderWidth + i * pnlWidth, 0);
                Size s = new Size(pnlWidth, colHeaderHeight);
                string name = string.Format("header_0_{0}", (i + 1).ToString());
                DevComponents.DotNetBar.PanelEx pnl = null;
                if (this.headerCells.ContainsKey(name))
                {
                    pnl = this.headerCells[name];
                    pnl.Location = p;
                    pnl.Size = s;
                    pnl.Visible = true;
                }
                else
                {
                    pnl = this.makePanel(name, (i + 1).ToString(), p, s);
                    this.headerCells.Add(name, pnl);
                }
                this.pnlContainer.Controls.Add(pnl);
            }

            //建立節次物件
            for (int j = 0; j < rowCount; j++)
            {
                Point p = new Point(0, colHeaderHeight + j * pnlHeight);
                Size s = new Size(rowHeaderWidth, pnlHeight);
                string name = string.Format("header_{0}_0", "" + Periods[j]);
                DevComponents.DotNetBar.PanelEx pnl = null;
                if (this.headerCells.ContainsKey(name))
                {
                    pnl = this.headerCells[name];
                    pnl.Location = p;
                    pnl.Size = s;
                    pnl.Visible = true;
                }
                else
                {
                    pnl = this.makePanel(name, "" + Periods[j], p, s);
                    this.headerCells.Add(name, pnl);
                }
                this.pnlContainer.Controls.Add(pnl);
            }
            #endregion

            #region Originize Cells
            for (int i = 0; i < colCount; i++)
            {
                for (int j = 0; j < rowCount; j++)
                {
                    string name = string.Format("{0}_{1}", (i + 1).ToString(), Periods[j]);
                    Point p = new Point(rowHeaderWidth + i * pnlWidth, colHeaderHeight + j * pnlHeight);
                    Size s = new Size(pnlWidth, pnlHeight);

                    DevComponents.DotNetBar.PanelEx pnl = null;
                    if (this.cells.ContainsKey(name))
                    {
                        pnl = this.cells[name];
                        pnl.Location = p;
                        pnl.Size = s;
                        pnl.Visible = true;                       
                    }
                    else
                    {
                        pnl = this.makePanel(name, "", p, s);
                        this.cells.Add(name, pnl);
                        DecPeriod dec = new DecPeriod(pnl, i + 1, Periods[j],GetSchedulerType(this.AssocObjType));
                        dec.BackColor = Color.White;
                        this.decPeriods.Add(name, dec);
                        dec.OnPeriodClicked += new PeriodClickedHandler(dec_OnPeriodClicked);
                    }

                    this.pnlContainer.Controls.Add(pnl);
                }
            }
            #endregion

            this.pnlContainer.ResumeLayout();
        }

        /// <summary>
        /// 重新填入DataGrid及畫面上相關的內容
        /// </summary>
        private void UpdateContent()
        {
            this.SelectedPeriods.ForEach(x => x.InitialContent(this.ttCur.TimeTableID));
            this.SelectedPeriods.Clear();

            Appointment appTest = null;
            int nSolution = 0;
            string idLast = string.Empty;
            bool bLastXchgable = false;
            Panel strEvtInfo = null;

            Stopwatch watch = Stopwatch.StartNew();

            watch.Stop();

            Console.WriteLine("" + watch.ElapsedMilliseconds);

            foreach(DecPeriod Period in this.decPeriods.Values)
                Period.InitialContent(ttCur.TimeTableID);

            #region  針對時間表當中的每個時段
            foreach (Period prdMember in ttCur.Periods)
            {
                //若節次不為0才繼續
                if (prdMember.PeriodNo != 0)
                {
                    string name = prdMember.WeekDay + "_" + prdMember.PeriodNo;

                    DecPeriod decPeriod = decPeriods[name];

                    bool IsSetWeekDayText = false;

                    //根據Period檢查Appointments是否有對應的空閒時間
                    appTest = apsCur.GetAppointment(
                        prdMember.WeekDay,
                        prdMember.BeginTime,
                        prdMember.Duration,
                        mOption.WeekFlag);

                    if (AssocObjType == Constants.lvWhere)
                    {
                        apsCur = new Appointments();

                        for (int i = 0; i < schLocal.Classrooms[mObjID].Capacity; i++)
                        {
                            schLocal.Classrooms[mObjID].UseAppointments(i); //預設使用第一個行事曆

                            foreach (Appointment App in schLocal.Classrooms[mObjID].Appointments)
                                apsCur.Add(App);
                        }
                    }

                    Appointments appTests = apsCur.GetAppointments(prdMember.WeekDay, prdMember.BeginTime, prdMember.Duration);

                    #region 時間表不排課時段
                    if (appTests == null || appTests.Count == 0)
                    {
                        if (prdMember.Disable)
                        {
                            string DisplayStr = prdMember.Disable ? prdMember.DisableMessage : string.Empty;
                            decPeriod.SetAsBusy(DisplayStr);
                            IsSetWeekDayText = true;
                        }
                    }
                    #endregion
                    #region 多門分課情況
                    else if (appTests.IsMultipleEvents())
                    {
                        List<CEvent> CEvents = new List<CEvent>();

                        for(int i=0;i<appTests.Count;i++)
                        {
                            if (!string.IsNullOrEmpty(appTests[i].EventID))
                            {
                                CEvent eventLocal = schLocal.CEvents[appTests[i].EventID];
                                CEvents.Add(eventLocal);
                            }
                        }

                        decPeriod.Data = CEvents;
                        IsSetWeekDayText = true;
                    }
                    #endregion
                    #endregion
                    else if (appTests.Count >= 1)
                    {
                        appTest = appTests[0];

                        #region 若EventID為空值，則為不排課時段
                        if (string.IsNullOrEmpty(appTest.EventID))
                        {
                            string BusyDesc = !string.IsNullOrEmpty(appTest.Description) ? appTest.Description : "不排課時段";
                            decPeriod.SetAsBusy(BusyDesc);
                            IsSetWeekDayText = true;
                        }
                        #endregion
                        else
                        {
                            #region 若Appointment的Timetable等於目前的TimeTable則顯示，否則顯示TimeTable名稱

                            string EventTimeTableID = GetTimeTableID(appTest.EventID);

                            if (EventTimeTableID == ttCur.TimeTableID)
                            {
                                CEvent eventLocal = schLocal.CEvents[appTest.EventID];
                                List<CEvent> DataSource = new List<CEvent>();
                                DataSource.Add(eventLocal);
                                decPeriod.Data = DataSource;

                                if (eventLocal.EventID.Equals(idTestEvent) &&
                                    !eventLocal.ManualLock)
                                    SelectedPeriods.Add(decPeriod);

                                IsSetWeekDayText = true;
                                #endregion
                            }
                            else //當資源的TimeTableID不等於Appointment的TimeTableID，那麼設定顯示名稱為Appointment的TimeTableID
                            {
                                CEvent eventLocal = schLocal.CEvents[appTest.EventID];
                                List<CEvent> Datasource = new List<CEvent>();
                                Datasource.Add(eventLocal);
                                decPeriod.Data = Datasource;
                                IsSetWeekDayText = true;
                            }
                            #endregion
                        }
                        idLast = appTest.EventID;
                    }

                    #region 查詢排課解
                    if ((!string.IsNullOrEmpty(idTestEvent) && !schLocal.CEvents[idTestEvent].ManualLock) ||
                        idTestEvents.Count > 0)
                    {
                        if (idTestEvents.Count > 0)
                        {
                            bool IsSchedule = true;

                            foreach (string vidTestEvent in idTestEvents)
                            {
                                if (GetTimeTableID(vidTestEvent) == ttCur.TimeTableID)
                                {
                                    IsSchedule = schLocal.IsSchedulable(vidTestEvent, prdMember.WeekDay, prdMember.PeriodNo);

                                    if (!IsSchedule)
                                    {
                                        if (!IsSetWeekDayText)
                                        {
                                            if (!string.IsNullOrEmpty(schLocal.ReasonDesc.AssocID))
                                                decPeriod.SetNoSolution(
                                                    schLocal.ReasonDesc.Desc, 
                                                    GetSchedulerType(schLocal.ReasonDesc.AssocType), 
                                                    schLocal.ReasonDesc.AssocID, 
                                                    schLocal.ReasonDesc.AssocName);
                                            else
                                                decPeriod.SetNoSolution(schLocal.ReasonDesc.Desc);
                                        }
                                        break;
                                    }
                                }
                            }

                            if (IsSchedule)
                            {
                                nSolution++;
                                decPeriod.SetSchedulable();
                            }
                        }
                        else if (GetTimeTableID(idTestEvent) == ttCur.TimeTableID)
                        {
                            bool IsSchedule = schLocal.IsSchedulable(idTestEvent, prdMember.WeekDay, prdMember.PeriodNo);

                            if (IsSchedule)
                            {
                                nSolution++;
                                decPeriod.SetSchedulable();
                            }
                            else
                            {
                                if (!IsSetWeekDayText)
                                {
                                    if (!string.IsNullOrEmpty(schLocal.ReasonDesc.AssocID))
                                        decPeriod.SetNoSolution(
                                            schLocal.ReasonDesc.Desc,
                                            GetSchedulerType(schLocal.ReasonDesc.AssocType),
                                            schLocal.ReasonDesc.AssocID,
                                            schLocal.ReasonDesc.AssocName);
                                    else
                                        decPeriod.SetNoSolution(schLocal.ReasonDesc.Desc);
                                }
                            }
                        }
                    }
                    #endregion
                }

                if ((appTest != null) && (prdMember.PeriodNo != 0))
                    idLast = appTest.EventID;
            }

            SelectedPeriods.ForEach(x => x.IsSelected = true);

            Console.WriteLine("" + watch.Elapsed.TotalMilliseconds);
        }

        ///// <summary>
        ///// 取得事件顯示字串
        ///// </summary>
        ///// <returns></returns>
        //private void SetEventDisplayStr(ucPeriod Period, string EventID)
        //{
        //    Period.AddEventControl(mOption, EventID, this.AssocObjType, this.AssocObjID);
        //}

        ///// <summary>
        ///// 取得顯示字串物件
        ///// </summary>
        ///// <param name="Message"></param>
        ///// <returns></returns>
        //private void SetDisplayStr(ucPeriod Period, string Message)
        //{
        //    Period.SetContent(1, Message);
        //}

        ///// <summary>
        ///// 取得場地顯示字串
        ///// </summary>
        ///// <returns></returns>
        //private void GetWhereDisplayStr(ucPeriod Period, List<string> EventIDs)
        //{
        //    string DisplayEventID = EventIDs[0];

        //    foreach (string EventID in EventIDs)
        //        if (EventID.Equals(idTestEvent))
        //            DisplayEventID = idTestEvent;


        //    Period.AddEventControl(mOption, DisplayEventID, this.AssocObjType, this.AssocObjID);
        //    Period.SetContent(2, "共有" + EventIDs.Count + "門分課");

        //    //Top = 0;

        //    //foreach (string EventID in EventIDs)
        //    //    Top = pnlTooltip.AddEventControl(mOption, EventID, this.AssocObjType, this.AssocObjID, Top);

        //    //pnlTooltip.Height = Top + Delta;
        //    //Panel.ToolTip = pnlTooltip;

        //    //return Panel;
        //}

        ///// <summary>
        ///// 取得群組課程字串
        ///// </summary>
        ///// <param name="EventIDs"></param>
        ///// <returns></returns>
        //private void SetGroupDisplayStr(ucPeriod Period, List<string> EventIDs)
        //{
        //    CEvent eventLocal = schLocal.CEvents[EventIDs[0]];

        //    //顯示群組名稱
        //    Period.SetContent(1, eventLocal.CourseGroup);

        //    //Label lbl = new Label();
        //    //lbl.HorizontalContentAlignment = System.Windows.HorizontalAlignment.Left;
        //    //lbl.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
        //    //lbl.VerticalAlignment = System.Windows.VerticalAlignment.Top;
        //    //lbl.VerticalContentAlignment = System.Windows.VerticalAlignment.Top;
        //    //lbl.Content = eventLocal.CourseGroup;

        //    //Canvas.SetLeft(lbl, Left);
        //    //Canvas.SetTop(lbl, Top);
        //    //Panel.Children.Add(lbl);

        //    //foreach (string EventID in EventIDs)
        //    //    Top = pnlTooltip.AddEventControl(mOption, EventID, this.AssocObjType, this.AssocObjID, Top);

        //    //pnlTooltip.Height = Top + Delta;
        //    //Panel.ToolTip = pnlTooltip;

        //    //return Panel;
        //}

        ///// <summary>
        ///// 取得單雙週顯示字串
        ///// </summary>
        ///// <param name="EventIDS"></param>
        ///// <param name="EventIDD"></param>
        ///// <returns></returns>
        //private void SetComplexDisplayStr(ucPeriod Period, string EventIDS, string EventIDD)
        //{
        //    int DefaultIndex = 1;

        //    Period.AddEventControl(mOption, EventIDS, this.AssocObjType, this.AssocObjID, DefaultIndex);
        //    Period.AddEventControl(mOption, EventIDD, this.AssocObjType, this.AssocObjID, DefaultIndex);
        //}

        #region LPView TestEvent
        /// <summary>
        /// 設定測試分課
        /// </summary>
        /// <param name="idEvent">分課系統編號</param>
        private void SetTestEvent(string idEvent)
        {
            //目前的測試事件編號等於傳入的事件編號，則不繼續執行
            if (idTestEvent == idEvent) return;

            //取得事件物件，並設定目前的測試事件編號
            CEvent evtTest = schLocal.CEvents[idEvent];
            idTestEvent = idEvent;
            idTestCourseID = evtTest.CourseID;
        }

        /// <summary>
        /// 清除測試分課
        /// </summary>
        private void ClearTestEvent()
        {
            colTestEvents.Clear();

            idTestEvent = string.Empty;
            Transfers.Clear();
        }

        /// <summary>
        /// 檢查分課表編號是否有存在測試分課表編號集合中
        /// </summary>
        /// <param name="EventID">分課表編號</param>
        /// <returns></returns>
        public bool TestEventExists(string EventID)
        {
            return colTestEvents.ContainsKey(EventID);
        }

        /// <summary>
        /// 加入測試分課表編號
        /// </summary>
        /// <param name="EventID">分課表編號</param>
        public void AddTestEvent(string EventID)
        {
            //判斷測試分課表是否存在
            bool bExists = colTestEvents.ContainsKey(EventID);

            #region 分課已存在則設為此分課
            if (bExists)
            {

            }
            #endregion
            #region 加入到測試清單
            else
            {
                if (IsRelatedEvent(EventID))
                {
                    //CEvents evtTests = new CEvents();
                    CEvent evtTest = schLocal.CEvents[EventID];

                    //evtTests.Add(evtTest);

                    //schLocal.GetSolutionCounts(evtTests);

                    //ListBoxItem NewItem = new ListBoxItem();
                    //NewItem.Cursor = Cursors.Hand;
                    //NewItem.Content = new usrTestEvent(evtTest);

                    Transfers.Add(evtTest);

                    //grdTestEvent.Items.Add(evtTest.TransferTo());


                    //lstTestEvent.Items.Add(NewItem);
                    colTestEvents.Add(evtTest.EventID, evtTest.Length); //加入到測試清單
                }
            }
            #endregion
        }

        /// <summary>
        /// 移除測試分課表編號
        /// </summary
        /// <param name="EventID">分課表編號</param>
        public void RemoveTestEvent(string EventID)
        {
            int nIndex = 0;
            bool bExists = colTestEvents.ContainsKey(EventID); //判斷測試事件集合是否包含分課表編號
            CEvent RemoveTestEvent = null;
            //若包含分課表編號則移除
            if (bExists)
            {
                //若測試分課表集合中包含分課表編號，就找出其索引位置
                for (nIndex = 0; nIndex < colTestEvents.Count; nIndex++)
                    if (colTestEvents.Keys.ToList()[nIndex] == EventID)
                        break;

                //將分課表編號自測試集合中移除
                colTestEvents.Remove(EventID);

                #region 自ListBox移除
                for (int i = 0; i < Transfers.Count; i++)
                {
                    //ListBoxItem Item = lstTestEvent.Items[i] as ListBoxItem;
                    //usrTestEvent TestEvent = Item.Content as usrTestEvent;
                    CEvent TestEvent = Transfers[i];

                    if (TestEvent.EventID.Equals(EventID))
                        RemoveTestEvent = TestEvent;
                }

                if (RemoveTestEvent != null)
                {
                    Transfers.Remove(RemoveTestEvent);
                }

                //if (SelectedIndex >= 0)
                //    grdTestEvent.Items.RemoveAt(SelectedIndex);
                //lstTestEvent.Items.RemoveAt(SelectedIndex);
                #endregion
            }

            //若沒有存在則離開
            if (!bExists)
                return;

            //若集合數量為0就清空測試分課表相關資訊，並重新繪製功課表
            if (colTestEvents.Count == 0)
            {
                ClearTestEvent();
                UpdateContent();
                return;
            }
        }

        /// <summary>
        /// 取得目前的測試分課表編號
        /// </summary>
        /// <returns></returns>
        public string CurrentTestEventID()
        {
            return idTestEvent;
        }
        #endregion

        #region Properties
        /// <summary>
        /// 資源類型，有lvWho、lvWhom及lvWhere
        /// </summary>
        public int AssocObjType
        {
            get { return mType; }
        }

        /// <summary>
        /// 資源編號
        /// </summary>
        public string AssocObjID
        {
            get { return mObjID; }
        }

        /// <summary>
        /// 取得目前時間表
        /// </summary>
        public string CurrentTimeTableID
        {
            get
            {
                if (ttCur != null)
                    return ttCur.TimeTableID;
                else
                    return string.Empty;
            }
        }
        #endregion
    }
}