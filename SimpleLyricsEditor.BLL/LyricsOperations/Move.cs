﻿using System;
using System.Collections.Generic;
using System.Linq;
using SimpleLyricsEditor.DAL;
using SimpleLyricsEditor.IBLL;

namespace SimpleLyricsEditor.BLL.LyricsOperations
{
    public class Move : ILyricsOperation
    {
        private readonly TimeSpan _interpolation;
        private readonly bool _isBig;

        public Move(TimeSpan targetTime, IList<Lyric> items)
        {
            TargetTime = targetTime;
            Items = items;

            var oldTime = Items.First().Time;
            _isBig = oldTime > TargetTime;
            _interpolation = _isBig ? oldTime - TargetTime : TargetTime - oldTime;
        }

        public IList<Lyric> Items { get; set; }
        public TimeSpan TargetTime { get; set; }

        public void Do()
        {
            foreach (var lyric in Items)
                lyric.Time = _isBig ? lyric.Time - _interpolation : lyric.Time + _interpolation;
        }

        public void Undo()
        {
            foreach (var lyric in Items)
                lyric.Time = !_isBig ? lyric.Time - _interpolation : lyric.Time + _interpolation;
        }
        
    }
}