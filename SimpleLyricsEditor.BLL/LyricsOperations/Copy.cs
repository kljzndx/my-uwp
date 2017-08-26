﻿using System;
using System.Collections.Generic;
using System.Linq;
using SimpleLyricsEditor.DAL;
using SimpleLyricsEditor.IBLL;

namespace SimpleLyricsEditor.BLL.LyricsOperations
{
    public class Copy : ILyricsOperation
    {
        private readonly TimeSpan _interpolation;
        private readonly bool _isBig;

        public Copy(IList<Lyric> items, IList<Lyric> targetList, TimeSpan targetTime)
        {
            Items = items;
            TargetList = targetList;
            TargetTime = targetTime;

            var oldTime = items.First().Time;
            _isBig = oldTime > targetTime;
            _interpolation = _isBig ? oldTime - targetTime : targetTime - oldTime;
        }

        public IList<Lyric> Items { get; set; }
        public IList<Lyric> TargetList { get; set; }
        public TimeSpan TargetTime { get; set; }

        public void Do()
        {
            foreach (var lyric in Items)
            {
                var time = _isBig ? lyric.Time - _interpolation : lyric.Time + _interpolation;
                TargetList.Add(new Lyric(time, lyric.Content));
            }
        }

        public void Undo()
        {
            foreach (var lyric in Items)
                TargetList.Remove(lyric);
        }
    }
}