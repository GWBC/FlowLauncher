﻿using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Droplex
{
    public static class Installer
    {
        /// <summary>
        /// Installs from the specified location using the passed in silent install arguements
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the install is unable to run correctly </exception>
        /// <exception cref="FileNotFoundException">Thrown when unable to manage the deletion/creaction of download directory </exception>
        /// <exception cref="OperationCanceledException">Thrown when the installation is cancelled or unsuccessful </exception>
        public static Task Install(string filepath, string installArgs)
        {
            // For the case where path contains a comma
            var path = '"' + filepath + '"';

            var psi = new ProcessStartInfo
            {
                UseShellExecute = true,
                FileName = path,
                Arguments = installArgs
            };
            var p = Process.Start(psi);

            return p.WaitForExitAsync();

        }

        private static Task WaitForExitAsync(this Process process)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            process.EnableRaisingEvents = true;
            process.Exited += ProcessExited;

            return tcs.Task;

            void ProcessExited(object sender, EventArgs e)
            {
                if (process.ExitCode == 0)
                {
                    tcs.TrySetResult(true);
                }
                else
                {
                    tcs.TrySetException(new OperationCanceledException());
                }
            }
        }
    }
}
