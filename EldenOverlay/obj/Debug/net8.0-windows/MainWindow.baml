   M S B A M L   `   `   `  ����  EldenEncouragement!    EldenRingOverlay.MainWindow   5   	   W SPresentationCore, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35\ XPresentationFramework, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35e aSystem.Windows.Controls.Ribbon, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089R NWindowsBase, Version=8.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35F 9http://schemas.microsoft.com/winfx/2006/xaml/presentation     5   	   R NSystem.Xaml, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089:x,http://schemas.microsoft.com/winfx/2006/xaml     5   	     � Title$  
MainWindow��5   	    � WindowStyle$
 None=�5   	    � AllowsTransparency$
 True��5   	    � 
ResizeMode$ NoResize=�5   	   
������  � Topmost$
 True��5	   	    � ShowInTaskbar$ False��5
   	    � WindowState$ 	Maximized=�5   	    � Triggers 5      :� 5   
    :� RoutedEvent$ Window.Loaded��6   .[�5      �� .��5      �� 	 g� RepeatBehavior$	 Forever��6   
 g� AutoReverse$

 True��66   .��5      W�  �� TargetProperty$ Opacity�6&    W� From 0.995   &   	 W� To 1.062    g� Duration$ 0:0:1D�5   &   5      5      5       � 	Resources 5         MedievalFont��%O   &	        *�5   
   1-pack://application:,,,/Fonts/#IM FELL English)�6Y   5      .��5      �� .+�5   
   �� -   5         ��Name$ AIEncouragement��5       J� Opacity$ 0Q�5      $��You are not hollow yet...��5       #��[  5!      $��18(�5"      
�������$
��Wrap=�5$       �� TextAlignment$ Center=�5%      $��Center=�5&      $��Center=�5'      �� 5*   
   -   5         ��Name$ 
LogTextBox��5+      $��12(�5,      $ 0Q�5-      
����������	#80000080 �� 
IsReadOnly$
 True��50      $��Bottom=�51      $	��150��52      $��10q�53      54      55      