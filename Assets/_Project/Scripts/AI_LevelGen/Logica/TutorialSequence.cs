using System;

namespace Ascua.LevelGen
{
    /// <summary>
    /// Avance de los pasos de un capítulo del tutorial (lógica pura, sin Unity, testeable).
    /// Un paso solo avanza si ya estuvo en pantalla su tiempo mínimo Y el jugador hizo lo que pide
    /// ("se avanza haciendo, no leyendo"). Los pasos no disponibles (p. ej. un Recuerdo que aún no se tiene)
    /// se saltan al llegar a ellos.
    /// </summary>
    public sealed class TutorialSequence
    {
        private readonly float[] minSeconds;

        public TutorialSequence(float[] minSeconds)
        {
            this.minSeconds = minSeconds ?? new float[0];
        }

        /// <summary>Paso actual (igual a Count al terminar).</summary>
        public int Index { get; private set; }

        /// <summary>Segundos que lleva en pantalla el paso actual.</summary>
        public float StepTime { get; private set; }

        public int Count
        {
            get { return minSeconds.Length; }
        }

        public bool Finished
        {
            get { return Index >= minSeconds.Length; }
        }

        /// <summary>Empieza desde el primer paso disponible. isAvailable puede ser null (todos disponibles).</summary>
        public void Begin(Func<int, bool> isAvailable)
        {
            Index = 0;
            StepTime = 0f;
            SkipUnavailable(isAvailable);
        }

        /// <summary>
        /// Avanza el tiempo del paso actual y, si ya pasó su mínimo y isDone() es true, pasa al siguiente disponible.
        /// isDone solo se consulta pasado el mínimo. Devuelve true si cambió de paso (o terminó).
        /// </summary>
        public bool Tick(float dt, Func<bool> isDone, Func<int, bool> isAvailable)
        {
            if (Finished) return false;
            if (dt > 0f) StepTime += dt;
            if (StepTime < minSeconds[Index]) return false;
            if (isDone == null || !isDone()) return false;

            Index++;
            StepTime = 0f;
            SkipUnavailable(isAvailable);
            return true;
        }

        /// <summary>Termina el capítulo ya (p. ej. si el jugador activa "Saltar tutorial").</summary>
        public void Skip()
        {
            Index = minSeconds.Length;
            StepTime = 0f;
        }

        private void SkipUnavailable(Func<int, bool> isAvailable)
        {
            if (isAvailable == null) return;
            while (Index < minSeconds.Length && !isAvailable(Index)) Index++;
        }
    }

    /// <summary>Qué capítulo del tutorial toca (lógica pura).</summary>
    public static class TutorialRules
    {
        public const int NoChapter = 0;
        /// <summary>Noche 1: moverse, recoger, el Farol, hablar, luciérnagas y Esperanza.</summary>
        public const int Night1Chapter = 1;
        /// <summary>Noche 2: esquivar, la luz contra las sombras, ataque cargado y contraataque.</summary>
        public const int Night2Chapter = 2;

        /// <param name="night">Noche que se juega.</param>
        /// <param name="skipTutorial">El jugador eligió "Saltar tutorial".</param>
        /// <param name="night1Pending">El capítulo de la Noche 1 aún no se completó (GameProgress.TutorialPending).</param>
        /// <param name="night2Done">El capítulo de la Noche 2 ya se completó en esta partida.</param>
        public static int ChapterFor(int night, bool skipTutorial, bool night1Pending, bool night2Done)
        {
            if (skipTutorial) return NoChapter;
            if (night <= 1) return night1Pending ? Night1Chapter : NoChapter;
            if (night == 2) return night2Done ? NoChapter : Night2Chapter;
            return NoChapter;
        }

        /// <summary>Un pensamiento "de una vez" (Cacería, Apagavelas) se muestra si está pasando, no se vio y no se salta el tutorial.</summary>
        public static bool ShowOneShot(bool skipTutorial, bool alreadySeen, bool happening)
        {
            return !skipTutorial && !alreadySeen && happening;
        }
    }
}
