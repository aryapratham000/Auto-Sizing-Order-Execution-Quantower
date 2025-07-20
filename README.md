***This project implements a smart buy tigger in C# for the Quantower trading platform. It is designed for use in discretionary setups where execution speed and risk consistency matter***

### 🧠 Core Logic

This strategy automates trade execution with built-in volatility-adjusted risk management. On launch, it immediately places a **Buy** order and automatically manages the following:

- Calculates stop loss distance using **ATR** (Average True Range) to adapt to current market volatility
- Uses a predefined **reward-to-risk ratio** to compute the take profit target
- Splits position size across **ES and MES contracts** to match the dollar risk allocation as closely as possible (e.g., 10 MES = 1 ES logic)
- Determines the number of contracts based on a fixed **dollar risk per trade**, ensuring consistent sizing across different volatility conditions
- Immediately submits both a **stop loss** and **take profit** order upon entry, using limit and stop order types respectively
- If either Take Profit or Stop Loss is hit, the strategy automatically cancels the other order and stops execution
- All trades and metrics are tracked internally, including:
  - Profit Factor
  - Max Drawdown
  - Net & Gross PnL
  - Win/Loss streaks
  - Total trades, win ratio, and fees
- Metrics are exported via `.NET Meter` for monitoring and logging

This strategy is ideal for traders who want to **manually choose when to enter**, but want the **sizing and exit logic to be handled automatically and objectively**.

### 🚀 Usage

1. Open the `.sln` file in **Visual Studio**
2. Build and deploy the strategy into **Quantower**
3. Set your desired risk configuration and symbol settings
4. When ready, start the strategy — it will handle execution and exits automatically


