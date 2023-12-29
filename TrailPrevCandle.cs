#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies.CustomStrategies
{
	public class TrailStop : Strategy
	{
		private Order entryOrder;
		private Order stopOrder;
		private string ENTRY_SIGNAL1;
		private string STOP_SIGNAL1;
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Strategy here.";
				Name										= "TrailStop";
				Calculate									= Calculate.OnPriceChange;
				EntriesPerDirection							= 1;
				EntryHandling								= EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy				= true;
				ExitOnSessionCloseSeconds					= 30;
				IsFillLimitOnTouch							= false;
				MaximumBarsLookBack							= MaximumBarsLookBack.Infinite;
				OrderFillResolution							= OrderFillResolution.Standard;
				Slippage									= 0;
				StartBehavior								= StartBehavior.WaitUntilFlat;
				TimeInForce									= TimeInForce.Gtc;
				TraceOrders									= false;
				RealtimeErrorHandling						= RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling							= StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade							= 20;
				IsDataSeriesRequired						= true;
				// Disable this property for performance gains in Strategy Analyzer optimizations
				// See the Help Guide for additional information
				IsInstantiatedOnEachOptimizationIteration	= true;
				TimeFrameInMin				= 0;
				TimeFrameInSec				= 0;
				StopOffSet					= 1;
				IsLongStrategy				= true;
			}
			else if (State == State.Configure)
			{
				
				entryOrder = null;
				stopOrder = null;
				ENTRY_SIGNAL1 = @"LongSignal1";
				STOP_SIGNAL1 = @"StopSignal1";
				StopOffSet = 1.0;
				
				if(TimeFrameInMin != 0 && TimeFrameInSec != 0) {
					throw new Exception("Invalid time frame configuration");
				}
				if(TimeFrameInMin != 0) {
					AddDataSeries(BarsPeriodType.Minute,TimeFrameInMin);
				} else {
					AddDataSeries(BarsPeriodType.Second,TimeFrameInSec);
				}
				
			}
			else if (State == State.Realtime) {
				if(entryOrder != null) entryOrder = GetRealtimeOrder(entryOrder);
				if(stopOrder != null) stopOrder = GetRealtimeOrder(stopOrder);
			}
		}

		protected override void OnBarUpdate()
		{
			if(BarsInProgress != 1) return;
			
			if(State != State.Realtime) return;
			if (CurrentBars[0] < BarsRequiredToTrade || CurrentBars[1] < BarsRequiredToTrade)
    			return;
			
			// go long after breakout
			if(entryOrder == null && Position.MarketPosition == MarketPosition.Flat && IsBreakOutOrBreakDown() ) {
				GoLongOrShort();
			} // update stop loss when new bar opens
			else if((Position.MarketPosition == MarketPosition.Long || Position.MarketPosition == MarketPosition.Short) && stopOrder != null && stopOrder.StopPrice != StopWithOffSet()) {
				//Print("retriggerin the stop "+stopOrder.StopPrice+" "+StopWithOffSet());
				AddStop(stopOrder);
			}
		}
		
		protected override void OnOrderUpdate(Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string nativeError)
		{
		  // One time only, as we transition from historical
		  // Convert any old historical order object references to the live order submitted to the real-time account
			if(order.Name == ENTRY_SIGNAL1) {
				entryOrder = order;
				
				if(order.OrderState == OrderState.Cancelled && order.Filled == 0) 
				{
					entryOrder = null;	
				}
			}
			else if(order.Name == STOP_SIGNAL1) {
				stopOrder = order;
			}
		}
		
		protected override void OnExecutionUpdate(Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
			if(entryOrder != null && entryOrder == execution.Order)
			{
				if(execution.Order.OrderState == OrderState.Filled) {
					stopOrder = IsLongStrategy ? ExitLongStopMarket(barsInProgressIndex: 0, true, execution.Order.Filled, StopWithOffSet(), STOP_SIGNAL1, ENTRY_SIGNAL1)
					: ExitShortStopMarket(barsInProgressIndex: 0, true, execution.Order.Filled, StopWithOffSet(), STOP_SIGNAL1, ENTRY_SIGNAL1);	
				}
			}
			
			if (execution.Order.OrderState != OrderState.PartFilled && execution.Order.Filled == 1)
            {
                entryOrder = null;
				
            }
		}
		
		#region Private
		private void GoLongOrShort()
		{
			if(IsLongStrategy) {
				EnterLong(1, ENTRY_SIGNAL1);
			} else {
				EnterShort(1, ENTRY_SIGNAL1);
			}
		}
		
		private void AddStop(Order stopOrder) {
			if(IsLongStrategy) {
				ExitLongStopMarket(barsInProgressIndex: 0, true, stopOrder.Quantity, StopWithOffSet(), STOP_SIGNAL1, ENTRY_SIGNAL1);
			} else {
				ExitShortStopMarket(barsInProgressIndex: 0, true, stopOrder.Quantity, StopWithOffSet(), STOP_SIGNAL1, ENTRY_SIGNAL1);
			}
		}
		
		private bool IsBreakOutOrBreakDown() 
		{
			return IsLongStrategy ? Closes[1][0] > Highs[1][1] : Closes[1][0] < Lows[1][1];	
		}
		
		private double StopWithOffSet()
		{
			return IsLongStrategy ? Lows[1][1]-StopOffSet : Highs[1][1] + StopOffSet;	
		}
		
		#endregion

		#region Properties
		[Display(Name="TimeFrameInMin", Description="timeframe of strategy", Order=1, GroupName="Parameters")]
		[Range(0, int.MaxValue), NinjaScriptProperty]
		public int TimeFrameInMin { get; set; }
		[Display(Name="TimeFrameInSec", Description="timeframe of strategy", Order=2, GroupName="Parameters")]
		[Range(0, int.MaxValue), NinjaScriptProperty]
		public int TimeFrameInSec { get; set; }
		[Display(Name="StopLossOffSet", Description="Howmuch offset to add ", Order=3, GroupName="Parameters")]
		public double StopOffSet { get; set; }
		[Display(Name="IsLongStrategy", Description="Long Strategy ", Order=4, GroupName="Parameters")]
		public bool IsLongStrategy {get; set; }
		[Display(Name="IsShortStrategy", Description="Short Strategy", Order=5, GroupName="Parameters")]
		public bool IsShortStrategy {get; set; }
		[Display(Name="LastNCandles", Description="Put stop at last N candles", Order=1, GroupName="Parameters")]
		public bool LastNCandles {get; set; }
		#endregion

	}
}
