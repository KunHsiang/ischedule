﻿using System.Collections.Generic;

namespace Sunset.Data
{
    /// <summary>
    /// 教師集合
    /// </summary>
    public class Teachers : IEnumerable<Teacher>
    {
        private Dictionary<string, Teacher> mTeachers;

        /// <summary>
        /// 建構式
        /// </summary>
        public Teachers()
        {
            //初始化教師集合
            mTeachers = new Dictionary<string, Teacher>();
        }

        /// <summary>
        /// 新增教師
        /// </summary>
        /// <param name="NewTeacher">教師物件</param>
        /// <returns>新增的教師物件，若是沒有新增成功則回傳null。</returns>
        public Teacher Add(Teacher NewTeacher)
        {
            //假設已有包含鍵值就傳回null
            if (mTeachers.ContainsKey(NewTeacher.TeacherID))
                return null;

            //將教師加入到集合中
            mTeachers.Add(NewTeacher.TeacherID,NewTeacher);

            //回傳新增的教師
            return NewTeacher;
        }

        /// <summary>
        /// 根據鍵值移除教師
        /// </summary>
        /// <param name="Key">教師編號</param>
        public void Remove(string Key)
        {
            //假設教師集合包含鍵值，就移除該教師
            if (mTeachers.ContainsKey(Key))
                mTeachers.Remove(Key);
        }

        /// <summary>
        /// 根據教師編號判斷在集合中是否有該教師
        /// </summary>
        /// <param name="TeacherID">教師編號</param>
        /// <returns>是否包含</returns>
        public bool Exists(string TeacherID)
        {
            return mTeachers.ContainsKey("" + TeacherID);
        }

        /// <summary>
        /// 根據鍵值從集合中取得集合
        /// </summary>
        /// <param name="Key">教師編號</param>
        /// <returns>教師</returns>
        public Teacher this[string Key]
        {
            get
            {
                //假設在教師集合中有包含該鍵值，就傳回該教師，否則傳回null
                return mTeachers.ContainsKey(Key) ? mTeachers[Key] : null;
            }
        }

        /// <summary>
        /// 教師集合數量
        /// </summary>
        public int Count { get { return mTeachers.Count; } }

        /// <summary>
        /// 有分課的教師數量
        /// </summary>
        public int HasTotalHourCount 
        {
            get 
            {
                int vHasTotalHourCount = 0;

                foreach (Teacher vTeacher in mTeachers.Values)
                    if (vTeacher.TotalHour > 0)
                        vHasTotalHourCount++;

                return vHasTotalHourCount; 
            }
        }

        /// <summary>
        /// 清除所有資料
        /// </summary>
        public void Clear()
        {
            mTeachers.Clear();
        }

        #region IEnumerable<Who> Members

        public IEnumerator<Teacher> GetEnumerator()
        {
            return mTeachers.Values.GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return mTeachers.Values.GetEnumerator();
        }

        #endregion
    }
}