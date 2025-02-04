using OpenSvip.Model;
using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Common;
using Note = OpenSvip.Model.Note;
using NPinyin;
using Melanchall.DryWetMidi.Interaction;

namespace FlutyDeer.MidiPlugin
{
    public class MidiEventsUtil
    {
        private short PPQ;

        public void SetPPQ(short PPQ)
        {
            this.PPQ = PPQ;
        }

        public int SemivowelPreShift { get; set; }
        public bool IsExportLyrics { get; set; }
        public bool IsUseCompatibleLyric { get; set; }
        public bool IsRemoveSymbols { get; set; }
        public int Transpose { get; set; }

        /// <summary>
        /// 将曲速标记列表转换为 MIDI 事件数组。
        /// </summary>
        /// <returns>含有曲速事件的 MIDI Event 数组。</returns>
        public MidiEvent[] SongTempoListToMidiEvents(List<SongTempo> songTempoList)
        {
            List<MidiEvent> midiEventList = new List<MidiEvent>();
            int PreviousEventTime = 0;
            foreach (var tempo in songTempoList)
            {
                midiEventList.Add(EncodeSetTempoEvent(tempo, ref PreviousEventTime));
                PreviousEventTime = tempo.Position;
            }
            return midiEventList.ToArray();
        }

        /// <summary>
        /// 将单个曲速标记转换为设置曲速 MIDI 事件。
        /// </summary>
        /// <param name="tempo">曲速。</param>
        /// <param name="PreviousEventTime">上一个曲速事件的绝对时间，单位为梯。</param>
        /// <returns>设置曲速 MIDI 事件。</returns>
        private SetTempoEvent EncodeSetTempoEvent(SongTempo tempo, ref int PreviousEventTime)
        {
            SetTempoEvent setTempoEvent = new SetTempoEvent
            {
                MicrosecondsPerQuarterNote = BPMToMicrosecondsPerQuarterNote((long)tempo.BPM),
                DeltaTime = tempo.Position - PreviousEventTime
            };
            PreviousEventTime = tempo.Position;
            return setTempoEvent;
        }

        /// <summary>
        /// 将曲速转换为每四分音符的微秒数。
        /// </summary>
        /// <param name="BPM">曲速。</param>
        /// <returns>每四分音符的微秒数。</returns>
        public long BPMToMicrosecondsPerQuarterNote(long BPM)
        {
            return (long)(60.0 / BPM * 1000000.0);
        }

        /// <summary>
        /// 转换演唱轨。
        /// </summary>
        /// <param name="singingTrack">原始演唱轨。</param>
        /// <returns>含有音符事件数组的 Track Chunk。</returns>
        public TrackChunk SingingTrackToMidiTrackChunk(SingingTrack singingTrack)
        {
            PreShiftUtil.PreShiftSemivowelNotes(singingTrack.NoteList, SemivowelPreShift);
            List<MidiEvent> midiEventList = new List<MidiEvent>();
            midiEventList.Add(new SequenceTrackNameEvent(singingTrack.Title));//写入轨道名称
            TrackChunk trackChunk = new TrackChunk(midiEventList.ToArray());
            using (var objectsManager = new TimedObjectsManager<TimedEvent>(trackChunk.Events))
            {
                var events = objectsManager.Objects;
                foreach(var note in singingTrack.NoteList)
                {
                    if(IsExportLyrics)
                    {
                        events.Add(new TimedEvent(new LyricEvent(GetLyric(note)), note.StartPos));
                    }
                    events.Add(new TimedEvent(new NoteOnEvent(GetTransposedKeyNumber(note), (SevenBitNumber)45), note.StartPos));
                    events.Add(new TimedEvent(new NoteOffEvent(GetTransposedKeyNumber(note), (SevenBitNumber)0), note.StartPos + note.Length));
                }
            }
            return trackChunk;
        }

        /// <summary>
        /// 根据输出选项转换歌词。
        /// </summary>
        /// <param name="note">音符。</param>
        /// <returns></returns>
        private string GetLyric(Note note)
        {
            string lyric;
            if (note.Pronunciation != null)
            {
                lyric = note.Pronunciation;
            }
            else
            {
                lyric = note.Lyric;
                if (lyric.Length > 1 && IsRemoveSymbols)
                {
                    foreach (var symbol in SymbolList.SymbolToRemoveList())
                    {
                        lyric = lyric.Replace(symbol, "");
                    }
                }
                if (IsUseCompatibleLyric)
                {
                    //lyric = Pinyin.GetPinyin(lyric);
                    lyric = new Pinyin().ConvertToPinyin(lyric);
                }
            }
            return lyric;
        }

        /// <summary>
        /// 获取移调后的音高。
        /// </summary>
        /// <param name="note">音符。</param>
        /// <returns>移调后的音高。</returns>
        private SevenBitNumber GetTransposedKeyNumber(Note note)
        {
            int transposedKeyNumber = note.KeyNumber + Transpose;
            if (transposedKeyNumber < 0)
            {
                transposedKeyNumber = 0;
            }
            else if (transposedKeyNumber > 127)
            {
                transposedKeyNumber = 127;
            }
            return (SevenBitNumber)transposedKeyNumber;
        }
    }
}