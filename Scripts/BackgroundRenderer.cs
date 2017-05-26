using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace BaroqueUI
{
    internal class BackgroundRenderer
    {
        public delegate void OnRenderDelegate();

        public OnRenderDelegate onRender;
        public bool inForeground
        {
            get { return m_queue != 0; }
            set { if (inForeground != value) { m_queue = value ? 1 : 0; Enqueue(); } }
        }
        private int m_queue;

        public BackgroundRenderer(OnRenderDelegate onRender, bool inForeground = false)
        {
            this.onRender = onRender;
            m_queue = inForeground ? 1 : 0;
            InitQueues();
            Enqueue();
        }

        public void Stop()
        {
            onRender = null;
        }

        static void InitQueues()
        {
            if (queues == null)
            {
                queues = new Queue<BackgroundRenderer>[2];
                queues[0] = new Queue<BackgroundRenderer>();
                queues[1] = new Queue<BackgroundRenderer>();
            }
        }

        void Enqueue()
        {
            queues[m_queue].Enqueue(this);
        }


        const float TIME_PER_FRAME = 0.003f;   /* 3 milliseconds */
        const float MAX_TIME_BUDGET = 1.7f * TIME_PER_FRAME;
        static Queue<BackgroundRenderer>[] queues;
        static float last_time = float.NegativeInfinity;
        static int next_queue;
        static float time_budget;

        public static void LateUpdate()
        {
            /* call this from LateUpdate().  The onRender() callback may be called. */

            if (Time.unscaledTime == last_time)
                return;    /* ignore all calls done during the same frame after the first one */

            last_time = Time.unscaledTime;

            time_budget += TIME_PER_FRAME;
            if (time_budget > MAX_TIME_BUDGET) time_budget = MAX_TIME_BUDGET;
            float timeout_time = Time.realtimeSinceStartup + time_budget;

            int queues_empty = 0;
            var reenqueue = new List<BackgroundRenderer>();

            while (queues_empty < 2 && Time.realtimeSinceStartup < timeout_time)
            {
                Queue<BackgroundRenderer> queue = queues[next_queue];
                next_queue = 1 - next_queue;

                if (queue.Count == 0)
                {
                    queues_empty++;
                    continue;
                }
                queues_empty = 0;

                BackgroundRenderer br = queue.Dequeue();
                if (br.onRender != null)
                {
                    reenqueue.Add(br);
                    try
                    {
                        br.onRender();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }

            foreach (var br in reenqueue)
                if (br.onRender != null)
                    queues[br.m_queue].Enqueue(br);

            time_budget = timeout_time - Time.realtimeSinceStartup;   /* can be <= 0 */
        }
    }
}