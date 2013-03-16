﻿using System;
using System.Collections.Generic;
using System.ServiceModel;
using System.Text;
using System.Windows;

namespace Patchy.IPC
{
    [ServiceContract]
    public interface ISingletonService
    {
        [OperationContract]
        void HandleArguments(string[] args);
        [OperationContract]
        void Shutdown();
    }
}
