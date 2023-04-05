# mTcping
A simple ping-over-tcp tool


```
wget https://t.mili.one/tcping -O /usr/bin/tcping
chmod +x /usr/bin/tcping 
```

```
mTcping - A simple Ping over TCP tool
Copyright (c) 2021 Milkey Tan. Code released under the MIT License

Usage: mTcping [options] <host> <port>

Arguments:
  host                Target host address
  port                Target host port

Options:
  -?|-h|--help        Show help information
  -t                  Tcping target host until you type Ctrl+C to stop
  -c|--count <count>  Number of echo requests to send
  -i <time>           Request interval to send
  -w <timeout>        Timeout time to wait for each reply
  -4                  Forced IPv4
  -6                  Forced IPv6
  -d                  Display response timestamp
  -s                  Stop on receipt of response

参数:
  host                指定的目标主机地址。
  port                指定的目标主机端口。

选项:
  -?|-h|--help        展示更多帮助信息。
  -t                  Tcping 指定的主机，直到键入 Ctrl+C 停止。
  -a|--async          Async Tcping 指定的主机，异步快速模式。
  -c|--count <count>  要发送的回显请求数。
  -i <time>           要发送的请求间隔时间。
  -w <timeout>        等待每次回复的超时时间(毫秒)。
  -4                  强制使用 IPv4。
  -6                  强制使用 IPv6。
  -d                  显示响应时间戳。
  -s                  在收到响应时停止。
```
