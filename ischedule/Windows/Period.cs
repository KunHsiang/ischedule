﻿using System;
using Sunset.Data;

namespace ischedule
{
    /// <summary>
    /// 代表一個時段，用來檢查時段是否有衝突
    /// </summary>
    public class SchPeriod
    {
        /// <summary>
        /// 星期
        /// </summary>
        public int Weekday { get; set; }

        /// <summary>
        /// 開始小時
        /// </summary>
        public int Hour { get; set; }

        /// <summary>
        /// 開始分鐘
        /// </summary>
        public int Minute { get; set; }

        /// <summary>
        /// 持續分鐘
        /// </summary>
        public int Duration { get; set; }

        /// <summary>
        /// 單雙週
        /// </summary>
        public int WeekFlag { get; set; }

        /// <summary>
        /// 用來對應資料列，若無需要可不使用
        /// </summary>
        public int Position { get; set; }

        /// <summary>
        /// 時段描述
        /// </summary>
        public string Desc { get; set; }

        /// <summary>
        /// 事件編號
        /// </summary>
        public string EventID { get; set; }
    }

    public static class PeriodConverter
    {
        public static Appointment ToAppointment(this SchPeriod vPeriod)
        {
            DateTime vDateTime = new DateTime(1900, 1, 1, vPeriod.Hour, vPeriod.Minute, 0);

            Appointment vAppointment = new Appointment(
                vPeriod.Weekday,
                vDateTime,
                vPeriod.Duration,
                3, string.Empty, string.Empty, string.Empty, vPeriod.Desc);

            return vAppointment;
        }

        public static TimeTableSec ToTimeTableSec(this Period vPeriod)
        {
            TimeTableSec Sec = new TimeTableSec();
            Sec.WeekDay = vPeriod.WeekDay;
            Sec.BeginTime = vPeriod.BeginTime;
            Sec.Duration = vPeriod.Duration;
            Sec.Period = vPeriod.PeriodNo;
            Sec.DispPeriod = vPeriod.DisplayPeriod;
            Sec.DisableMessage = vPeriod.DisableMessage;

            return Sec;
        }

        public static SchPeriod ToPeriod(this Appointment vApp)
        {
            SchPeriod Period = new SchPeriod();
            Period.Weekday = vApp.WeekDay;
            Period.Hour = vApp.BeginTime.Hour;
            Period.Minute = vApp.BeginTime.Minute;
            Period.Duration = vApp.Duration;
            Period.Position = 0;
            Period.WeekFlag = 3;
            Period.Desc = vApp.Description;
            Period.EventID = vApp.EventID;

            return Period;
        }

        public static SchPeriod ToPeriod(this TimeTableSec vTimeTableSec)
        {
            SchPeriod Period = new SchPeriod();
            Period.Weekday = vTimeTableSec.WeekDay;
            Period.Hour = vTimeTableSec.BeginTime.Hour;
            Period.Minute = vTimeTableSec.BeginTime.Minute;
            Period.Duration = vTimeTableSec.Duration;
            Period.Desc = vTimeTableSec.DisableMessage;
            Period.Position = 0;
            Period.WeekFlag = 3;

            return Period;
        }

        //public static Period ToPeriod(this ClassroomBusy vClassroomBusy)
        //{
        //    Period Period = new Period();
        //    Period.Weekday = vClassroomBusy.WeekDay;
        //    Period.Hour = vClassroomBusy.BeginTime.Hour;
        //    Period.Minute = vClassroomBusy.BeginTime.Minute;
        //    Period.Duration = vClassroomBusy.Duration;
        //    Period.Position = 0;
        //    Period.WeekFlag = 3;
        //    Period.Desc = vClassroomBusy.BusyDesc;

        //    return Period; 
        //}

        //public static Period ToPeriod(this TeacherExBusy vTeacherBusy)
        //{
        //    Period Period = new Period();
        //    Period.Weekday = vTeacherBusy.WeekDay;
        //    Period.Hour = vTeacherBusy.BeginTime.Hour;
        //    Period.Minute = vTeacherBusy.BeginTime.Minute;
        //    Period.Duration = vTeacherBusy.Duration;
        //    Period.Position = 0;
        //    Period.WeekFlag = 3;
        //    Period.Desc = vTeacherBusy.BusyDesc;

        //    return Period;
        //}

        //public static Period ToPeriod(this ClassExBusy vClassBusy)
        //{
        //    Period Period = new Period();
        //    Period.Weekday = vClassBusy.WeekDay;
        //    Period.Hour = vClassBusy.BeginTime.Hour;
        //    Period.Minute = vClassBusy.BeginTime.Minute;
        //    Period.Duration = vClassBusy.Duration;
        //    Period.Position = 0;
        //    Period.WeekFlag = 3;
        //    Period.Desc = vClassBusy.BusyDesc;

        //    return Period;
        //}

        //public static ClassroomBusy ToClassroomBusy(this Period Period, int ClassroomID)
        //{
        //    ClassroomBusy vClassroomBusy = new ClassroomBusy();
        //    vClassroomBusy.ClassroomID = ClassroomID;
        //    vClassroomBusy.WeekDay = Period.Weekday;
        //    vClassroomBusy.BeginTime = new DateTime(1900, 1, 1, Period.Hour, Period.Minute, 0);
        //    vClassroomBusy.Duration = Period.Duration;
        //    vClassroomBusy.BusyDesc = Period.Desc;

        //    return vClassroomBusy;
        //}

        //public static ClassExBusy ToClassExBusy(this Period Period,int ClassID)
        //{
        //    ClassExBusy vClassBusy = new ClassExBusy();
        //    vClassBusy.ClassID = ClassID;
        //    vClassBusy.WeekDay = Period.Weekday;
        //    vClassBusy.BeginTime = new DateTime(1900,1,1,Period.Hour,Period.Minute,0);
        //    vClassBusy.Duration = Period.Duration;
        //    vClassBusy.BusyDesc = Period.Desc;

        //    return vClassBusy;
        //}

        //public static TeacherExBusy ToTeacherExBusy(this Period Period, int TeacherID)
        //{
        //    TeacherExBusy vTeacherBusy = new TeacherExBusy();
        //    vTeacherBusy.TeacherID = TeacherID;
        //    vTeacherBusy.WeekDay = Period.Weekday;
        //    vTeacherBusy.BeginTime = new DateTime(1900, 1, 1, Period.Hour, Period.Minute, 0);
        //    vTeacherBusy.Duration = Period.Duration;
        //    vTeacherBusy.BusyDesc = Period.Desc;

        //    return vTeacherBusy;
        //}
    }
}