// Pinch distance calculation adapted from Jermesa-Studio/JrsOrbitCamera.cs (main, read 2026-10-11).
// MIT License
// Copyright (c) 2023 Samborlang Pyrtuh
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using UnityEngine;

namespace BrickDefense.Presentation
{
    // Gesture state is independent of Input so the same touch frames can be replayed.
    internal sealed class BoardTouchControls
    {
        private readonly Action<Vector2> rotate,select;
        private readonly Action<float> zoom;
        private readonly Func<Vector2,bool> overUI;
        private int firstId=-1,secondId=-1;
        private Vector2 start,last;
        private bool dragging,pinching,waitForLift;

        public BoardTouchControls(Action<Vector2> rotate,Action<float> zoom,Action<Vector2> select,Func<Vector2,bool> overUI)
        { this.rotate=rotate; this.zoom=zoom; this.select=select; this.overUI=overUI; }

        public void Cancel() { firstId=secondId=-1; dragging=pinching=false; waitForLift=true; }

        // 单指在抬起时选格，超过阈值才旋转；双指捏合只控制缩放。
        public void Process(int count,Touch first,Touch second,Vector2 screenSize)
        {
            if(count==0) { Cancel(); waitForLift=false; return; }
            // 手势被取消后必须等全部手指离屏，防止捏合尾帧误触选格。
            if(waitForLift) return;
            if(count>2) { Cancel(); return; }
            Vector2 p=Gui(first.position,screenSize.y);
            if(!Inside(p,screenSize) || first.phase==TouchPhase.Canceled) { Cancel(); return; }

            if(count==2)
            {
                Vector2 q=Gui(second.position,screenSize.y);
                if(!Inside(q,screenSize) || overUI(p) || overUI(q) ||
                    first.phase==TouchPhase.Ended || second.phase==TouchPhase.Ended || second.phase==TouchPhase.Canceled)
                { Cancel(); return; }
                if(!pinching)
                {
                    if((firstId==-1 && first.phase!=TouchPhase.Began && second.phase!=TouchPhase.Began) ||
                        (firstId!=-1 && first.fingerId!=firstId && second.fingerId!=firstId))
                    { Cancel(); return; }
                    firstId=first.fingerId; secondId=second.fingerId; pinching=true; dragging=false;
                    return; // Establish the pair without applying the first frame's movement.
                }
                if(!((first.fingerId==firstId && second.fingerId==secondId) ||
                    (first.fingerId==secondId && second.fingerId==firstId)))
                { Cancel(); return; }
                Vector2 firstPrevious=first.position-first.deltaPosition;
                Vector2 secondPrevious=second.position-second.deltaPosition;
                float previousDistance=(firstPrevious-secondPrevious).magnitude;
                float distance=(first.position-second.position).magnitude;
                if(previousDistance>8 && distance>8 && Mathf.Abs(distance-previousDistance)>0.01f)
                    zoom(Mathf.Log(distance/previousDistance)/Mathf.Log(1.15f));
                return;
            }

            if(pinching) { Cancel(); return; }
            if(firstId==-1)
            {
                if(first.phase!=TouchPhase.Began || overUI(p)) { Cancel(); return; }
                firstId=first.fingerId; start=last=p; dragging=false; return;
            }
            if(first.fingerId!=firstId || first.phase==TouchPhase.Began) { Cancel(); return; }
            float threshold=Mathf.Max(8,Mathf.Min(screenSize.x,screenSize.y)*0.015f);
            dragging|=(p-start).sqrMagnitude>threshold*threshold;
            if(first.phase==TouchPhase.Ended)
            {
                if(!dragging && !overUI(p)) select(p);
                Cancel(); return;
            }
            Vector2 delta=p-last;
            if(dragging && delta.sqrMagnitude>0) rotate(delta);
            last=p;
        }

        private static Vector2 Gui(Vector2 position,float height)=>new Vector2(position.x,height-position.y);
        private static bool Inside(Vector2 p,Vector2 size)=>p.x>=0 && p.y>=0 && p.x<size.x && p.y<size.y;
    }
}
